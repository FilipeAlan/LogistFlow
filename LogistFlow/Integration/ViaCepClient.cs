using System.Text.Json;

namespace LogistFlow;

internal sealed record PostalAddress(string PostalCode, string Street, string District, string City, string State,
    string Complement = "");

internal sealed record AddressLookupResult(PostalAddress? Address, string? Error);

internal sealed class ViaCepClient(HttpClient httpClient)
{
    private const string InvalidResponse = "ViaCEP returned an invalid address response.";

    internal static bool TryNormalizePostalCode(string? value, out string normalized)
    {
        var code = value?.Trim() ?? "";
        if (code.Length == 9 && code[5] == '-')
            code = code.Remove(5, 1);

        if (code.Length == 8 && code.All(char.IsAsciiDigit))
        {
            normalized = code;
            return true;
        }
        normalized = "";
        return false;
    }

    internal async Task<AddressLookupResult> LookupAsync(string postalCode, CancellationToken cancellationToken = default)
    {
        if (!TryNormalizePostalCode(postalCode, out var code))
            return new(null, "Enter a postal code with eight digits, optionally formatted as 00000-000.");

        try
        {
            using var response = await httpClient.GetAsync($"ws/{code}/json/", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(null, $"ViaCEP returned HTTP {(int)response.StatusCode}.");

            using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            var data = await JsonSerializer.DeserializeAsync<ViaCepResponse>(body, JsonSerializerOptions.Web, cancellationToken);
            return ReadAddress(data, code);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(null, "The ViaCEP address lookup timed out.");
        }
        catch (JsonException)
        {
            return new(null, InvalidResponse);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            return new(null, "Could not read the address from ViaCEP. Check the network connection.");
        }
    }

    private static AddressLookupResult ReadAddress(ViaCepResponse? data, string requestedCode)
    {
        if (data is null)
            return new(null, InvalidResponse);

        if (data.Erro.ValueKind != JsonValueKind.Undefined)
        {
            if (!bool.TryParse(data.Erro.ToString(), out bool notFound))
                return new(null, InvalidResponse);
            if (notFound)
                return new(null, "ViaCEP could not find that postal code.");
        }

        if (!TryNormalizePostalCode(data.Cep, out var code) || code != requestedCode)
            return new(null, InvalidResponse);

        var address = new PostalAddress(code, data.Logradouro?.Trim() ?? "", data.Bairro?.Trim() ?? "",
            data.Localidade?.Trim() ?? "", data.Uf?.Trim() ?? "", data.Complemento?.Trim() ?? "");
        if (address is { Street: "", District: "", City: "", State: "", Complement: "" })
            return new(null, InvalidResponse);

        return new(address, null);
    }

    private sealed record ViaCepResponse(string? Cep = null, string? Logradouro = null, string? Bairro = null,
        string? Localidade = null, string? Uf = null, string? Complemento = null, JsonElement Erro = default);
}
