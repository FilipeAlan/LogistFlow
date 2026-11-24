namespace LogistFlow;

internal sealed class TerminalMenu(CollectionService service)
{
    private const string Menu = """

        LogistFlow
        1. Register collection
        2. List collections
        3. Filter by status
        4. Filter by priority
        5. Update collection status by ID
        6. Display operational summary
        7. Exit
        """;

    internal async Task RunAsync()
    {
        while (true)
        {
            Console.WriteLine(Menu);
            switch (TerminalInput.Read("Choose an option: "))
            {
                case "1": await RegisterAsync(); break;
                case "2": ShowCollections(); break;
                case "3": ShowCollections(status: TerminalInput.Choose<CollectionStatus>("Status")); break;
                case "4": ShowCollections(priority: TerminalInput.Choose<Priority>("Priority")); break;
                case "5": await UpdateStatusAsync(); break;
                case "6": PrintSummary(service.GetSummary()); break;
                case "7": return;
                default: Console.WriteLine("Invalid menu option. Choose a number from 1 to 7."); break;
            }
        }
    }

    private async Task RegisterAsync()
    {
        string name = TerminalInput.Required("Customer name: ");
        string address = await AddressPrompt.ReadAsync(service);
        int packages = TerminalInput.Number("Package count: ");
        var priority = TerminalInput.Choose<Priority>("Priority");
        var registered = await service.RegisterAsync(name, address, packages, priority);
        if (registered is null)
            Console.Error.WriteLine(service.State.LastError);
        else
            Console.WriteLine($"Registered collection #{registered.Id}.");
    }

    private async Task UpdateStatusAsync()
    {
        int id = TerminalInput.Number("Collection ID: ");
        if (!service.State.Collections.Any(item => item.Id == id))
        {
            Console.WriteLine("Collection not found.");
            return;
        }
        var status = TerminalInput.Choose<CollectionStatus>("New status");
        if (await service.UpdateStatusAsync(id, status))
            Console.WriteLine($"Updated collection #{id} to {status}.");
        else
            Console.Error.WriteLine(service.State.LastError);
    }

    private void ShowCollections(CollectionStatus? status = null, Priority? priority = null)
    {
        service.State.SetFilters(status, priority);
        bool found = false;
        foreach (var item in service.State.VisibleCollections)
        {
            Console.WriteLine($"#{item.Id} | {item.CustomerName} | {item.CollectionAddress} | Packages: {item.PackageCount} | Priority: {item.Priority} | Status: {item.Status}");
            found = true;
        }
        if (!found)
            Console.WriteLine("No collections found.");
    }

    private static void PrintSummary(OperationalSummary summary)
    {
        Console.WriteLine($"Total collections: {summary.TotalCollections}");
        Console.WriteLine($"Total packages: {summary.TotalPackages}");
        PrintCounts("Collections by priority", summary.ByPriority);
        PrintCounts("Collections by status", summary.ByStatus);
    }

    private static void PrintCounts<T>(string label, IReadOnlyDictionary<T, int> counts) where T : struct, Enum
    {
        Console.WriteLine(label);
        foreach (var key in Enum.GetValues<T>())
            Console.WriteLine($"  {key}: {counts[key]}");
    }
}
