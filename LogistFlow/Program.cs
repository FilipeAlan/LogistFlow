namespace LogistFlow;

internal static class Program
{
    private static async Task<int> Main()
    {
        var file = new CollectionFile();
        try
        {
            using var session = file.OpenSession();
            using var http = new HttpClient
            {
                BaseAddress = new Uri("https://viacep.com.br/"),
                Timeout = TimeSpan.FromSeconds(5)
            };
            var service = new CollectionService(file, new ViaCepClient(http));
            if (!await service.LoadAsync())
            {
                Console.Error.WriteLine(service.State.LastError);
                return 1;
            }
            await new TerminalMenu(service).RunAsync();
            return 0;
        }
        catch (EndOfStreamException)
        {
            Console.WriteLine("Input ended. Exiting LogistFlow.");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Cannot access collection data. Check directory permissions and close any other LogistFlow session using this directory.");
            return 1;
        }
    }
}
