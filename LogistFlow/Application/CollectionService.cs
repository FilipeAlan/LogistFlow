using System.Text.Json;

namespace LogistFlow;

internal sealed class CollectionService(CollectionFile file, ViaCepClient viaCep)
{
    private const string ReadError = "Could not read data/collections.json. Check file permissions and try again.";
    private const string SaveError = "Could not save collections. Check free space and write permissions. The change was not applied.";
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private bool loaded;

    public ApplicationState State { get; } = new();

    public Task<bool> LoadAsync() => RunFileOperationAsync(async () =>
    {
        loaded = false;
        State.ReplaceCollections(await file.LoadAsync());
        loaded = true;
        return true;
    }, false, ReadError);

    public Task<Collection?> RegisterAsync(string customerName, string collectionAddress, int packageCount, Priority priority)
        => RunFileOperationAsync<Collection?>(async () =>
        {
            EnsureLoaded();
            int lastId = State.Collections.Count == 0 ? 0 : State.Collections.Max(item => item.Id);
            if (lastId == int.MaxValue)
                throw new CollectionValidationException("No more collection IDs are available.");

            var collection = new Collection(lastId + 1, customerName?.Trim() ?? "", collectionAddress?.Trim() ?? "",
                packageCount, priority, CollectionStatus.Pending);
            collection.Validate();
            await SaveAsync([.. State.Collections, collection]);
            return collection;
        }, null, SaveError);

    public Task<bool> UpdateStatusAsync(int id, CollectionStatus status) => RunFileOperationAsync(async () =>
    {
        EnsureLoaded();
        var updated = State.Collections.ToList();
        int index = updated.FindIndex(item => item.Id == id);
        if (index == -1)
            throw new CollectionValidationException("Collection not found.");

        updated[index] = updated[index] with { Status = status };
        updated[index].Validate();
        await SaveAsync(updated);
        return true;
    }, false, SaveError);

    public Task<AddressLookupResult> LookupAddressAsync(string postalCode, CancellationToken cancellationToken = default)
        => RunAsync(async () =>
        {
            var result = await viaCep.LookupAsync(postalCode, cancellationToken);
            if (result.Error is not null)
                State.RecordError(result.Error);
            return result;
        }, cancellationToken);

    public OperationalSummary GetSummary() => OperationalSummary.From(State.Collections);

    private void EnsureLoaded()
    {
        if (!loaded)
            throw new CollectionValidationException("Load the collection file successfully before making changes.");
    }

    private async Task SaveAsync(List<Collection> updated)
    {
        await file.SaveAsync(updated);
        State.ReplaceCollections(updated);
    }

    private Task<T> RunFileOperationAsync<T>(Func<Task<T>> operation, T failure, string ioError) => RunAsync(async () =>
    {
        try
        {
            return await operation();
        }
        catch (CollectionValidationException ex)
        {
            State.RecordError(ex.Message);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            State.RecordError("Invalid data in data/collections.json. Check the file before restarting.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            State.RecordError(ioError);
        }
        return failure;
    });

    private async Task<T> RunAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        await operationGate.WaitAsync(cancellationToken);
        State.BeginOperation();
        try
        {
            return await operation();
        }
        finally
        {
            State.EndOperation();
            operationGate.Release();
        }
    }
}
