namespace LogistFlow;

internal enum Priority { Low, Medium, High }

internal enum CollectionStatus { Pending, InRoute, Completed }

internal sealed record Collection(
    int Id,
    string CustomerName,
    string CollectionAddress,
    int PackageCount,
    Priority Priority,
    CollectionStatus Status);
