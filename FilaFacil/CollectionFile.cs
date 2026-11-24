using System.Text.Json;
using System.Text.Json.Serialization;

namespace FilaFacil;

internal static class CollectionFile
{
    private const string DataDirectory = "data";
    private static readonly string FilePath = Path.Combine(DataDirectory, "collections.json");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        RespectRequiredConstructorParameters = true,
        AllowDuplicateProperties = false,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static FileStream OpenSession()
    {
        Directory.CreateDirectory(DataDirectory);
        return File.Open(Path.Combine(DataDirectory, "collections.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public static List<Collection> Load()
    {
        try
        {
            using var file = File.OpenRead(FilePath);
            var collections = JsonSerializer.Deserialize<List<Collection>>(file, JsonOptions)
                ?? throw new InvalidDataException("Expected an array of collections.");
            Validate(collections);
            return collections;
        }
        catch (FileNotFoundException)
        {
            return [];
        }
    }

    public static void Save(List<Collection> collections)
    {
        var temporaryPath = FilePath + ".tmp";
        using (var file = File.Create(temporaryPath))
        {
            JsonSerializer.Serialize(file, collections, JsonOptions);
            file.Flush(flushToDisk: true);
        }

        File.Move(temporaryPath, FilePath, overwrite: true);
    }

    private static void Validate(List<Collection> collections)
    {
        var ids = new HashSet<int>();
        foreach (var collection in collections)
        {
            if (collection is null || collection.Id <= 0 || collection.PackageCount <= 0 ||
                string.IsNullOrWhiteSpace(collection.CustomerName) ||
                string.IsNullOrWhiteSpace(collection.CollectionAddress) ||
                !Enum.IsDefined(collection.Priority) || !Enum.IsDefined(collection.Status))
            {
                throw new InvalidDataException("A collection has missing or invalid values.");
            }

            if (!ids.Add(collection.Id))
            {
                throw new InvalidDataException($"Duplicate collection ID {collection.Id}.");
            }
        }
    }
}
