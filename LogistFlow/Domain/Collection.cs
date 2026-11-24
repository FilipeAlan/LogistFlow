namespace LogistFlow;

internal enum Priority { Low, Medium, High }

internal enum CollectionStatus { Pending, InRoute, Completed }

internal sealed record Collection(
    int Id,
    string CustomerName,
    string CollectionAddress,
    int PackageCount,
    Priority Priority,
    CollectionStatus Status)
{
    internal void Validate()
    {
        if (Id <= 0)
            throw new CollectionValidationException("Collection ID must be positive.");
        if (string.IsNullOrWhiteSpace(CustomerName))
            throw new CollectionValidationException("Customer name is required.");
        if (string.IsNullOrWhiteSpace(CollectionAddress))
            throw new CollectionValidationException("Collection address is required.");
        if (PackageCount <= 0)
            throw new CollectionValidationException("Package count must be a positive whole number.");
        if (!Enum.IsDefined(Priority))
            throw new CollectionValidationException("Choose a valid collection priority.");
        if (!Enum.IsDefined(Status))
            throw new CollectionValidationException("Choose a valid collection status.");
    }
}

internal sealed class CollectionValidationException(string message) : Exception(message);
