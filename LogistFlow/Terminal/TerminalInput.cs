namespace LogistFlow;

internal static class TerminalInput
{
    internal static string Read(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine()?.Trim() ?? throw new EndOfStreamException();
    }

    internal static string Required(string prompt)
    {
        while (true)
        {
            string value = Read(prompt);
            if (value.Length > 0)
                return value;
            Console.WriteLine("A value is required.");
        }
    }

    internal static int Number(string prompt, int maximum = int.MaxValue)
    {
        while (true)
        {
            if (int.TryParse(Read(prompt), out int value) && value > 0 && value <= maximum)
                return value;
            Console.WriteLine(maximum == int.MaxValue
                ? "Enter a positive whole number."
                : $"Enter a whole number from 1 to {maximum}.");
        }
    }

    internal static T Choose<T>(string label) where T : struct, Enum
    {
        var choices = Enum.GetValues<T>();
        var options = choices.Select((value, index) => $"{index + 1}. {value}");
        Console.WriteLine($"{label} ({string.Join(", ", options)})");
        return choices[Number("Choose a number: ", choices.Length) - 1];
    }
}
