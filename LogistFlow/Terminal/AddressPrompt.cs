namespace LogistFlow;

internal static class AddressPrompt
{
    internal static async Task<string> ReadAsync(CollectionService service)
    {
        string digits;
        while (!ViaCepClient.TryNormalizePostalCode(TerminalInput.Read("Brazilian postal code (CEP): "), out digits))
            Console.WriteLine("Enter an eight-digit postal code, such as 01001000 or 01001-000.");

        string postalCode = $"CEP {digits[..5]}-{digits[5..]}";
        Console.WriteLine("Looking up the address with ViaCEP...");
        var lookup = await service.LookupAddressAsync(digits);
        if (lookup.Address is not {} address)
        {
            Console.WriteLine(lookup.Error);
            Console.WriteLine("Enter the collection address manually.");
            string manualAddress = TerminalInput.Required("Full collection address (without the CEP): ");
            return Join(manualAddress, postalCode);
        }

        Console.WriteLine($"ViaCEP: {Join(address.Street, address.Complement, address.District, address.City, address.State)}");
        string street = address.Street.Length > 0 ? address.Street : TerminalInput.Required("Street or collection location: ");
        string district = Complete(address.District, "District");
        string city = Complete(address.City, "City");
        string state = Complete(address.State, "State");
        string details = TerminalInput.Read("Number or additional address details (optional): ");
        return Join(street, details, address.Complement, district, city, state, postalCode);
    }

    private static string Complete(string value, string label) =>
        value.Length > 0 ? value : TerminalInput.Read($"{label} (optional): ");

    private static string Join(params string[] parts) =>
        string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
}
