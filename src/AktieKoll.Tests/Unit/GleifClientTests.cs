using System.Net;
using System.Text;
using AktieKoll.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static AktieKoll.Tests.Shared.TestHelpers.ServiceTestHelpers;

namespace AktieKoll.Tests.Unit;

public class GleifClientTests
{
    private static GleifClient Create(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(new HttpClient(new StubHandler(body, status)) { BaseAddress = new Uri("https://api.gleif.org") },
            NullLogger<GleifClient>.Instance);

    [Fact]
    public async Task GetIsins_ParsesDataAttributes()
    {
        var client = Create("""{"data":[{"attributes":{"isin":"SE0009554454"}}]}""");

        var isins = await client.GetIsinsAsync("LEI1", Ct);

        isins.Should().Equal("SE0009554454");
    }

    [Fact]
    public async Task GetIsins_HttpError_ReturnsEmpty()
    {
        var client = Create("oops", HttpStatusCode.InternalServerError);

        (await client.GetIsinsAsync("LEI1", Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetIsins_InvalidJson_ReturnsEmpty()
    {
        var client = Create("not json");

        (await client.GetIsinsAsync("LEI1", Ct)).Should().BeEmpty();
    }

    private sealed class StubHandler(string body, HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }
}
