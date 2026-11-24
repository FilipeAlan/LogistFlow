using System.Text.Json;

namespace LogistFlow;

internal static class Program
{
    private static int Main()
    {
        try
        {
            using var session = CollectionFile.OpenSession();
            var collections = CollectionFile.Load();
            RunMenu(collections);
            return 0;
        }
        catch (EndOfStreamException)
        {
            Console.WriteLine("Input ended. Exiting LogistFlow.");
            return 0;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            Console.Error.WriteLine("Invalid data in data/collections.json. Check the file before restarting.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Cannot access collection data. Check directory permissions and close any other LogistFlow session using this directory.");
        }
        return 1;
    }

    private static void RunMenu(List<Collection> collections)
    {
        while (true)
        {
            Console.WriteLine("""

                LogistFlow
                1. Register collection
                2. List collections
                3. Filter by status
                4. Filter by priority
                5. Update collection status by ID
                6. Display operational summary
                7. Exit
                """);
            switch (ReadLine("Choose an option: "))
            {
                case "1":
                    collections = Register(collections);
                    break;
                case "2":
                    ListCollections(collections);
                    break;
                case "3":
                    var status = Choose<CollectionStatus>("Status");
                    ListCollections(collections.Where(item => item.Status == status));
                    break;
                case "4":
                    var priority = Choose<Priority>("Priority");
                    ListCollections(collections.Where(item => item.Priority == priority));
                    break;
                case "5":
                    collections = UpdateStatus(collections);
                    break;
                case "6":
                    PrintSummary(collections);
                    break;
                case "7":
                    return;
                default:
                    Console.WriteLine("Invalid menu option. Choose a number from 1 to 7.");
                    break;
            }
        }
    }

    private static List<Collection> Register(List<Collection> collections)
    {
        int lastId = collections.Count == 0 ? 0 : collections.Max(item => item.Id);
        if (lastId == int.MaxValue)
        {
            Console.WriteLine("No more collection IDs are available.");
            return collections;
        }

        var collection = new Collection(
            lastId + 1,
            ReadRequired("Customer name: "),
            ReadRequired("Collection address: "),
            ReadNumber("Package count: "),
            Choose<Priority>("Priority"),
            CollectionStatus.Pending);
        return SaveChange(collections, [.. collections, collection], $"Registered collection #{collection.Id}.");
    }

    private static List<Collection> UpdateStatus(List<Collection> collections)
    {
        int id = ReadNumber("Collection ID: ");
        var collection = collections.Find(item => item.Id == id);
        if (collection is null)
        {
            Console.WriteLine("Collection not found.");
            return collections;
        }

        var status = Choose<CollectionStatus>("New status");
        var updated = collections.Select(item => item.Id == id ? item with { Status = status } : item).ToList();
        return SaveChange(collections, updated, $"Updated collection #{id} to {status}.");
    }

    private static List<Collection> SaveChange(List<Collection> current, List<Collection> updated, string message)
    {
        try
        {
            CollectionFile.Save(updated);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Could not save collections. Check free space and write permissions. The change was not applied.");
            return current;
        }
        Console.WriteLine(message);
        return updated;
    }

    private static void ListCollections(IEnumerable<Collection> collections)
    {
        bool found = false;
        foreach (var item in collections)
        {
            Console.WriteLine($"#{item.Id} | {item.CustomerName} | {item.CollectionAddress} | Packages: {item.PackageCount} | Priority: {item.Priority} | Status: {item.Status}");
            found = true;
        }
        if (!found)
            Console.WriteLine("No collections found.");
    }

    private static void PrintSummary(List<Collection> collections)
    {
        Console.WriteLine($"Total collections: {collections.Count}");
        Console.WriteLine($"Total packages: {collections.Sum(item => (long)item.PackageCount)}");
        Console.WriteLine("Collections by priority");
        PrintCounts(collections.Select(item => item.Priority));
        Console.WriteLine("Collections by status");
        PrintCounts(collections.Select(item => item.Status));
    }

    private static void PrintCounts<T>(IEnumerable<T> values) where T : struct, Enum
    {
        var counts = values.GroupBy(value => value).ToDictionary(group => group.Key, group => group.Count());
        foreach (var value in Enum.GetValues<T>())
            Console.WriteLine($"  {value}: {counts.GetValueOrDefault(value)}");
    }

    private static T Choose<T>(string label) where T : struct, Enum
    {
        var choices = Enum.GetValues<T>();
        var options = choices.Select((value, index) => $"{index + 1}. {value}");
        Console.WriteLine($"{label} ({string.Join(", ", options)})");
        return choices[ReadNumber("Choose a number: ", choices.Length) - 1];
    }

    private static int ReadNumber(string prompt, int maximum = int.MaxValue)
    {
        while (true)
        {
            if (int.TryParse(ReadLine(prompt), out int value) && value > 0 && value <= maximum)
                return value;
            Console.WriteLine(maximum == int.MaxValue
                ? "Enter a positive whole number."
                : $"Enter a whole number from 1 to {maximum}.");
        }
    }

    private static string ReadRequired(string prompt)
    {
        while (true)
        {
            string value = ReadLine(prompt);
            if (value.Length > 0)
                return value;
            Console.WriteLine("A value is required.");
        }
    }

    private static string ReadLine(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine()?.Trim() ?? throw new EndOfStreamException();
    }
}
