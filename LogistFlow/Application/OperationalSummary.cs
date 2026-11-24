namespace LogistFlow;

internal sealed record OperationalSummary(
    int TotalCollections,
    long TotalPackages,
    IReadOnlyDictionary<Priority, int> ByPriority,
    IReadOnlyDictionary<CollectionStatus, int> ByStatus)
{
    internal static OperationalSummary From(IReadOnlyList<Collection> collections) => new(
        collections.Count,
        collections.Sum(item => (long)item.PackageCount),
        CountGroups(collections.Select(item => item.Priority)),
        CountGroups(collections.Select(item => item.Status)));

    private static Dictionary<T, int> CountGroups<T>(IEnumerable<T> values) where T : struct, Enum
    {
        var counts = Enum.GetValues<T>().ToDictionary(value => value, _ => 0);
        foreach (var group in values.GroupBy(value => value))
            counts[group.Key] = group.Count();
        return counts;
    }
}
