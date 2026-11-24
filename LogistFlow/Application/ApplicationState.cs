namespace LogistFlow;

internal sealed class ApplicationState
{
    public IReadOnlyList<Collection> Collections { get; private set; } = [];
    public bool IsLoading { get; private set; }
    public string? LastError { get; private set; }
    public CollectionStatus? ActiveStatusFilter { get; private set; }
    public Priority? ActivePriorityFilter { get; private set; }

    public IEnumerable<Collection> VisibleCollections => Collections.Where(item =>
        (!ActiveStatusFilter.HasValue || item.Status == ActiveStatusFilter) &&
        (!ActivePriorityFilter.HasValue || item.Priority == ActivePriorityFilter));

    public void SetFilters(CollectionStatus? status = null, Priority? priority = null)
    {
        ActiveStatusFilter = status;
        ActivePriorityFilter = priority;
    }

    internal void BeginOperation()
    {
        IsLoading = true;
        LastError = null;
    }

    internal void EndOperation() => IsLoading = false;
    internal void RecordError(string message) => LastError = message;
    internal void ReplaceCollections(List<Collection> collections) => Collections = collections.AsReadOnly();
}
