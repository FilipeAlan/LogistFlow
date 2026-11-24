using System.Net;
using System.Text;

namespace LogistFlow.Tests;

public sealed class CollectionServiceTests : IDisposable
{
    private readonly string dataDirectory = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
    private readonly HttpClient http;
    private readonly CollectionFile file;
    private readonly CollectionService service;
    private string FilePath => Path.Combine(dataDirectory, "collections.json");

    public CollectionServiceTests()
    {
        Directory.CreateDirectory(dataDirectory);
        file = new CollectionFile(dataDirectory);
        http = new HttpClient(new TestHandler((_, _) => throw new InvalidOperationException("Unexpected HTTP request.")))
        {
            BaseAddress = new Uri("https://viacep.com.br/")
        };
        service = new CollectionService(file, new ViaCepClient(http));
    }

    [Fact]
    public async Task Registration_starts_pending_and_updates_existing_state_only_after_saving()
    {
        Assert.True(await service.LoadAsync());
        var state = service.State;
        var first = await service.RegisterAsync("Jo", "West Road", 3, Priority.High);
        Assert.NotNull(first);
        Assert.Equal(new Collection(1, "Jo", "West Road", 3, Priority.High, CollectionStatus.Pending), first);
        Assert.Same(state, service.State);
        Assert.Equal(first, Assert.Single(state.Collections));
        Assert.Equal(first, Assert.Single(await file.LoadAsync()));
        Assert.False(state.IsLoading);
        Assert.Null(state.LastError);
    }

    [Theory]
    [InlineData("", "West Road", 1, 0)]
    [InlineData("   ", "West Road", 1, 0)]
    [InlineData("Jo", "", 1, 0)]
    [InlineData("Jo", "  ", 1, 0)]
    [InlineData("Jo", "West Road", 0, 0)]
    [InlineData("Jo", "West Road", -2, 0)]
    [InlineData("Jo", "West Road", 1, 42)]
    public async Task Invalid_registration_reports_error_without_writing(string customer, string address, int count, int priority)
    {
        Assert.True(await service.LoadAsync());
        Assert.Null(await service.RegisterAsync(customer, address, count, (Priority)priority));
        Assert.Empty(service.State.Collections);
        Assert.False(File.Exists(FilePath));
        Assert.False(string.IsNullOrWhiteSpace(service.State.LastError));
        Assert.False(service.State.IsLoading);
    }

    [Fact]
    public async Task A_status_update_must_target_an_existing_ID_and_a_defined_status()
    {
        Assert.True(await service.LoadAsync());
        await service.RegisterAsync("Jo", "West Road", 3, Priority.High);
        Assert.False(await service.UpdateStatusAsync(2, CollectionStatus.Completed));
        Assert.NotNull(service.State.LastError);
        Assert.False(await service.UpdateStatusAsync(1, (CollectionStatus)99));
        Assert.Equal(CollectionStatus.Pending, Assert.Single(await file.LoadAsync()).Status);

        Assert.True(await service.UpdateStatusAsync(1, CollectionStatus.Completed));
        Assert.Null(service.State.LastError);
        Assert.Equal(CollectionStatus.Completed, Assert.Single(service.State.Collections).Status);
        Assert.Equal(CollectionStatus.Completed, Assert.Single(await file.LoadAsync()).Status);
    }

    [Fact]
    public async Task Filters_select_matching_rows_leave_the_full_list_intact_and_can_be_cleared()
    {
        Assert.True(await service.LoadAsync());
        await service.RegisterAsync("Ana", "First Road", 2, Priority.High);
        await service.RegisterAsync("Ben", "Second Road", 4, Priority.Low);
        await service.RegisterAsync("Cal", "Third Road", 6, Priority.High);
        Assert.True(await service.UpdateStatusAsync(3, CollectionStatus.InRoute));
        var state = service.State;

        state.SetFilters(status: CollectionStatus.Pending);
        Assert.Equal(CollectionStatus.Pending, state.ActiveStatusFilter);
        Assert.Null(state.ActivePriorityFilter);
        Assert.Equal(new[] { 1, 2 }, state.VisibleCollections.Select(c => c.Id));
        state.SetFilters(priority: Priority.High);
        Assert.Equal(Priority.High, state.ActivePriorityFilter);
        Assert.Null(state.ActiveStatusFilter);
        Assert.Equal(new[] { 1, 3 }, state.VisibleCollections.Select(c => c.Id));
        state.SetFilters(CollectionStatus.InRoute, Priority.High);
        Assert.Equal(3, Assert.Single(state.VisibleCollections).Id);
        state.SetFilters(CollectionStatus.Completed, Priority.High);
        Assert.Empty(state.VisibleCollections);
        Assert.Equal(3, state.Collections.Count);
        state.SetFilters();
        Assert.Equal(state.Collections, state.VisibleCollections);
    }

    [Fact]
    public async Task Summary_counts_all_priorities_and_statuses_including_zero_and_uses_long_totals()
    {
        Assert.True(await service.LoadAsync());
        var empty = service.GetSummary();
        Assert.Equal(0, empty.TotalPackages);
        Assert.All(Enum.GetValues<Priority>(), p => Assert.Equal(0, empty.ByPriority[p]));
        Assert.All(Enum.GetValues<CollectionStatus>(), s => Assert.Equal(0, empty.ByStatus[s]));

        await service.RegisterAsync("Ana", "First Road", int.MaxValue, Priority.Low);
        await service.RegisterAsync("Ben", "Second Road", 4, Priority.High);
        await service.RegisterAsync("Cal", "Third Road", 6, Priority.High);
        Assert.True(await service.UpdateStatusAsync(2, CollectionStatus.InRoute));
        Assert.True(await service.UpdateStatusAsync(3, CollectionStatus.Completed));
        service.State.SetFilters(priority: Priority.Low);

        var summary = service.GetSummary();
        Assert.Equal(3, summary.TotalCollections);
        Assert.Equal((long)int.MaxValue + 10, summary.TotalPackages);
        Assert.Equal(1, summary.ByPriority[Priority.Low]);
        Assert.Equal(0, summary.ByPriority[Priority.Medium]);
        Assert.Equal(2, summary.ByPriority[Priority.High]);
        Assert.All(Enum.GetValues<CollectionStatus>(), s => Assert.Equal(1, summary.ByStatus[s]));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Lookup_stays_loading_until_the_response_finishes_and_leaves_collections_untouched(bool succeeds)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proceed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new TestHandler(async (_, token) =>
        {
            entered.SetResult();
            await proceed.Task.WaitAsync(token);
            return new HttpResponseMessage(succeeds ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("""{"cep":"01001-000","logradouro":"Praça da Sé","bairro":"Sé","localidade":"São Paulo","uf":"SP"}""", Encoding.UTF8, "application/json")
            };
        })) { BaseAddress = new Uri("https://viacep.com.br/") };
        var application = new CollectionService(file, new ViaCepClient(client));
        Assert.True(await application.LoadAsync());
        var lookup = application.LookupAddressAsync("01001-000");
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(application.State.IsLoading);
            Assert.False(lookup.IsCompleted);
            Assert.Empty(application.State.Collections);
            Assert.False(File.Exists(FilePath));
        }
        finally { proceed.TrySetResult(); }

        await lookup;
        Assert.False(application.State.IsLoading);
        Assert.Equal(succeeds, application.State.LastError is null);
        Assert.Empty(application.State.Collections);
        Assert.False(File.Exists(FilePath));
        Assert.NotNull(await application.RegisterAsync("Jo", "Manual or confirmed address", 1, Priority.Low));
        Assert.Null(application.State.LastError);
    }

    [Fact]
    public async Task Save_failure_preserves_durable_and_in_memory_state_and_allows_a_retry()
    {
        Assert.True(await service.LoadAsync());
        await service.RegisterAsync("Jo", "West Road", 3, Priority.High);
        var before = await File.ReadAllBytesAsync(FilePath);
        var obstruction = Directory.CreateDirectory(FilePath + ".tmp");
        Assert.False(await service.UpdateStatusAsync(1, CollectionStatus.Completed));
        Assert.Equal(CollectionStatus.Pending, Assert.Single(service.State.Collections).Status);
        Assert.Null(await service.RegisterAsync("Unsaved", "East Road", 4, Priority.Low));
        Assert.Single(service.State.Collections);
        Assert.False(service.State.IsLoading);
        Assert.NotNull(service.State.LastError);
        Assert.Equal(before, await File.ReadAllBytesAsync(FilePath));

        obstruction.Delete();
        var next = await service.RegisterAsync("Ben", "East Road", 4, Priority.Low);
        Assert.Equal(2, next?.Id);
        Assert.Null(service.State.LastError);
        Assert.Equal(service.State.Collections, await file.LoadAsync());
    }

    [Fact]
    public async Task Canceling_a_lookup_clears_loading_and_releases_the_next_operation()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        using var client = new HttpClient(new TestHandler(async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("The fake request should end through cancellation.");
        })) { BaseAddress = new Uri("https://viacep.com.br/") };
        var application = new CollectionService(file, new ViaCepClient(client));
        Assert.True(await application.LoadAsync());
        var lookup = application.LookupAddressAsync("01001-000", cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(application.State.IsLoading);
        }
        finally { cancellation.Cancel(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lookup);
        Assert.False(application.State.IsLoading);
        Assert.Empty(application.State.Collections);
        Assert.NotNull(await application.RegisterAsync("Jo", "West Road", 1, Priority.High));
    }

    [Fact]
    public async Task Concurrent_registrations_keep_every_record_and_allocate_distinct_IDs()
    {
        Assert.True(await service.LoadAsync());
        var writes = Enumerable.Range(1, 6).Select(i =>
            service.RegisterAsync($"Customer {i}", $"Road {i}", i, Priority.Medium));
        var saved = await Task.WhenAll(writes);
        Assert.Equal(Enumerable.Range(1, 6), saved.Select(c => c!.Id).Order());
        var persisted = await file.LoadAsync();
        Assert.Equal(Enumerable.Range(1, 6), persisted.Select(c => c.PackageCount).Order());
        Assert.Equal(service.State.Collections, persisted);
    }

    [Fact]
    public async Task Read_failure_disables_changes_until_data_can_be_loaded_again()
    {
        Assert.Null(await service.RegisterAsync("Not loaded", "Road", 1, Priority.Low));
        Directory.CreateDirectory(FilePath);
        Assert.False(await service.LoadAsync());
        Assert.Contains("Could not read", service.State.LastError);
        Assert.False(service.State.IsLoading);
        Assert.Null(await service.RegisterAsync("Jo", "West Road", 1, Priority.Low));
        Directory.Delete(FilePath);
        Assert.True(await service.LoadAsync());
        Assert.Null(service.State.LastError);
        Assert.NotNull(await service.RegisterAsync("Jo", "West Road", 1, Priority.Low));
    }

    [Fact]
    public async Task Exhausted_IDs_leave_the_saved_file_intact()
    {
        await file.SaveAsync([new Collection(int.MaxValue, "Ben", "Last Road", 4, Priority.High, CollectionStatus.Pending)]);
        var previous = await File.ReadAllBytesAsync(FilePath);
        Assert.True(await service.LoadAsync());
        Assert.Null(await service.RegisterAsync("Cal", "Road", 6, Priority.High));
        Assert.NotNull(service.State.LastError);
        Assert.False(service.State.IsLoading);
        Assert.Equal(previous, await File.ReadAllBytesAsync(FilePath));
    }

    [Fact]
    public async Task Module_01_records_load_without_migration_and_new_ID_uses_the_highest_saved_ID()
    {
        const string original = """
            [
              {"Id":19,"CustomerName":"Ana","CollectionAddress":"Legacy Road 10","PackageCount":2,"Priority":"Medium","Status":"Completed"},
              {"Id":3,"CustomerName":"Ben","CollectionAddress":"Legacy Road 20","PackageCount":4,"Priority":"High","Status":"Pending"}
            ]
            """;
        await File.WriteAllTextAsync(FilePath, original);
        Assert.True(await service.LoadAsync());
        Assert.Equal(original, await File.ReadAllTextAsync(FilePath));
        var added = await service.RegisterAsync("Cal", "New Road", 6, Priority.High);
        Assert.Equal(20, added?.Id);

        var restarted = new CollectionService(new CollectionFile(dataDirectory), new ViaCepClient(http));
        Assert.True(await restarted.LoadAsync());
        Assert.Equal(service.State.Collections, restarted.State.Collections);
        Assert.Equal("Legacy Road 10", restarted.State.Collections[0].CollectionAddress);
        Assert.Equal(CollectionStatus.Completed, restarted.State.Collections[0].Status);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("[{}]")]
    [InlineData("[null]")]
    [InlineData("null")]
    [InlineData("""[{"Id":0,"CustomerName":"Jo","CollectionAddress":"Road","PackageCount":1,"Priority":"Low","Status":"Pending"}]""")]
    [InlineData("""[{"Id":1,"CustomerName":"Jo","CollectionAddress":"Road","PackageCount":0,"Priority":"Low","Status":"Pending"}]""")]
    [InlineData("""[{"Id":1,"CustomerName":"Jo","CollectionAddress":"Road","PackageCount":1,"Priority":"Low","Status":"Missing"}]""")]
    [InlineData("""[{"Id":1,"CustomerName":"Jo","CollectionAddress":"Road","PackageCount":1,"Priority":"Low","Status":"Pending"},{"Id":1,"CustomerName":"Ben","CollectionAddress":"Other","PackageCount":2,"Priority":"High","Status":"Completed"}]""")]
    public async Task Damaged_or_invalid_files_block_changes_and_are_preserved(string invalid)
    {
        await File.WriteAllTextAsync(FilePath, invalid);
        Assert.False(await service.LoadAsync());
        Assert.False(service.State.IsLoading);
        Assert.False(string.IsNullOrWhiteSpace(service.State.LastError));
        Assert.Null(await service.RegisterAsync("Jo", "West Road", 1, Priority.Medium));
        Assert.False(await service.UpdateStatusAsync(1, CollectionStatus.Completed));
        Assert.Equal(invalid, await File.ReadAllTextAsync(FilePath));
    }

    [Fact]
    public void Sessions_exclude_other_writers_and_release_the_lock_on_disposal()
    {
        using (file.OpenSession())
        {
            var second = new CollectionFile(dataDirectory);
            Assert.Throws<IOException>(() => second.OpenSession());
        }
        using var reopened = file.OpenSession();
    }

    public void Dispose()
    {
        http.Dispose();
        Directory.Delete(dataDirectory, recursive: true);
    }

    private sealed class TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }
}
