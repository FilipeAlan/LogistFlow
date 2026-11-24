using System.Net;
using System.Text;
using Xunit;

namespace LogistFlow.Tests;

public sealed class ViaCepClientTests
{
    [Fact]
    public async Task Lookup_sends_normalized_code_and_reads_address()
    {
        using var http = CreateClient((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://postal.test/ws/01001000/json/", request.RequestUri?.AbsoluteUri);
            return Task.FromResult(Json("""
                {"cep":"01001-000","logradouro":" Praça da Sé ","bairro":"Sé","localidade":"São Paulo","uf":"SP","complemento":" lado ímpar "}
                """));
        });

        var result = await new ViaCepClient(http).LookupAsync(" 01001-000 ");

        Assert.Null(result.Error);
        Assert.Equal(new PostalAddress("01001000", "Praça da Sé", "Sé", "São Paulo", "SP", "lado ímpar"), result.Address);
    }

    [Fact]
    public async Task Partial_response_keeps_available_fields_for_manual_completion()
    {
        using var http = CreateClient((_, _) => Task.FromResult(Json("""
            {"cep":"01001000","logradouro":null,"localidade":"São Paulo","uf":"SP"}
            """)));

        var result = await new ViaCepClient(http).LookupAsync("01001000");

        Assert.Null(result.Error);
        Assert.Equal(new PostalAddress("01001000", "", "", "São Paulo", "SP"), result.Address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0100-1000")]
    [InlineData("01 001000")]
    [InlineData("0100100A")]
    [InlineData("010010009")]
    [InlineData("０１００１０００")]
    public async Task Invalid_format_is_rejected_before_sending_http(string input)
    {
        using var http = CreateClient((_, _) => throw new InvalidOperationException("Unexpected HTTP request."));

        var result = await new ViaCepClient(http).LookupAsync(input);

        Assert.Null(result.Address);
        Assert.Contains("eight digits", result.Error);
    }

    [Theory]
    [InlineData("""{"erro":"true"}""")]
    [InlineData("""{"erro":true}""")]
    public async Task Not_found_response_is_reported(string json)
    {
        using var http = CreateClient((_, _) => Task.FromResult(Json(json)));
        var result = await new ViaCepClient(http).LookupAsync("99999999");
        Assert.Null(result.Address);
        Assert.Contains("could not find", result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{"localidade":"São Paulo"}""")]
    [InlineData("""{"cep":"99999-999","localidade":"São Paulo"}""")]
    [InlineData("""{"cep":"01001-000","bairro":42}""")]
    [InlineData("""{"cep":"01001-000","erro":"maybe","uf":"SP"}""")]
    [InlineData("""{"cep":"01001-000","logradouro":" "}""")]
    public async Task Unusable_json_response_is_reported_without_guessing_an_address(string json)
    {
        using var http = CreateClient((_, _) => Task.FromResult(Json(json)));
        var result = await new ViaCepClient(http).LookupAsync("01001000");
        Assert.Null(result.Address);
        Assert.Contains("invalid address response", result.Error);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Http_errors_report_the_status_code(HttpStatusCode status)
    {
        using var http = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        var result = await new ViaCepClient(http).LookupAsync("01001000");
        Assert.Null(result.Address);
        Assert.Contains($"HTTP {(int)status}", result.Error);
    }

    [Fact]
    public async Task Network_failure_offers_a_recoverable_error()
    {
        using var http = CreateClient((_, _) => throw new HttpRequestException("Simulated network failure."));
        var result = await new ViaCepClient(http).LookupAsync("01001000");
        Assert.Null(result.Address);
        Assert.Contains("network", result.Error);
    }

    [Fact]
    public async Task Timeout_is_reported_as_a_lookup_failure()
    {
        using var http = CreateClient((_, _) => throw new TaskCanceledException("Simulated HTTP timeout."));
        var result = await new ViaCepClient(http).LookupAsync("01001000");
        Assert.Null(result.Address);
        Assert.Contains("timed out", result.Error);
    }

    [Fact]
    public async Task Cancellation_requested_by_the_caller_propagates()
    {
        using var http = CreateClient((_, token) =>
        {
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Expected cancellation.");
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ViaCepClient(http).LookupAsync("01001000", cancellation.Token));
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static HttpClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new StubHandler(send)) { BaseAddress = new Uri("https://postal.test/") };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
