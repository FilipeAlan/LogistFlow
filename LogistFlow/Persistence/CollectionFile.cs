using System.Text.Json;
using System.Text.Json.Serialization;

namespace LogistFlow;

internal sealed class CollectionFile(string dataDirectory = "data")
{
    private readonly string filePath = Path.Combine(dataDirectory, "collections.json");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        RespectRequiredConstructorParameters = true,
        AllowDuplicateProperties = false,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public FileStream OpenSession()
    {
        Directory.CreateDirectory(dataDirectory);
        return File.Open(Path.Combine(dataDirectory, "collections.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public async Task<List<Collection>> LoadAsync()
    {
        try
        {
            await using var file = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var collections = await JsonSerializer.DeserializeAsync<List<Collection>>(file, JsonOptions)
                ?? throw new InvalidDataException("Expected an array of collections.");
            Validate(collections);
            return collections;
        }
        catch (FileNotFoundException)
        {
            return [];
        }
        catch (CollectionValidationException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    public async Task SaveAsync(IReadOnlyList<Collection> collections)
    {
        var temporaryPath = filePath + ".tmp";
        await using (var file = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write,
            FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(file, collections, JsonOptions);
            await file.FlushAsync();
        }

        File.Move(temporaryPath, filePath, overwrite: true);
    }

    private static void Validate(List<Collection> collections)
    {
        var ids = new HashSet<int>();
        foreach (var collection in collections)
        {
            if (collection is null)
                throw new InvalidDataException("A collection record is missing.");
            collection.Validate();
            if (!ids.Add(collection.Id))
            {
                throw new InvalidDataException($"Duplicate collection ID {collection.Id}.");
            }
        }
    }
}
