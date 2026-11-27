using System.Net;
using AktieKoll.Data;
using AktieKoll.Dtos;
using AktieKoll.Models;
using AktieKoll.Tests.Fixture;
using AktieKoll.Tests.Integration.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AktieKoll.Tests.Integration.Controllers;

public class ShortPositionsControllerTests(WebApplicationFactoryFixture factory) : IntegrationTestBase(factory)
{
    private static string TestId() => Guid.NewGuid().ToString("N")[..8].ToUpper();

    private async Task SeedAsync(string lei, string name, string symbol, decimal pct)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ShortIssuers.Add(new ShortIssuer { Lei = lei, Name = name, PrimarySymbol = symbol });
        db.ShortIssuerSymbols.Add(new ShortIssuerSymbol { Lei = lei, Symbol = symbol });
        db.ShortSnapshots.Add(new ShortSnapshot
        {
            Lei = lei,
            SnapshotDate = DateTime.UtcNow.Date,
            PositionPercent = pct,
            LatestPositionDate = DateTime.UtcNow.Date
        });
        await db.SaveChangesAsync(Token);
    }

    [Fact]
    public async Task Top_Default_ReturnsHighestFirst()
    {
        var id = TestId();
        await SeedAsync($"LEIHI{id}", $"High {id}", $"HI{id}", 900.5m);
        await SeedAsync($"LEILO{id}", $"Low {id}", $"LO{id}", 800.5m);

        var response = await Client.GetTestAsync("/api/shortpositions/top");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonTestAsync<ShortTopListDto>();
        var ours = dto!.Items.Where(i => i.Lei.EndsWith(id)).ToList();
        ours.Select(i => i.Lei).Should().Equal($"LEIHI{id}", $"LEILO{id}");
    }

    [Theory]
    [InlineData("sort=bogus")]
    [InlineData("sort=1")]
    [InlineData("days=0")]
    [InlineData("days=366")]
    public async Task Top_InvalidSort_Returns400(string query)
    {
        var response = await Client.GetTestAsync($"/api/shortpositions/top?{query}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Top_LimitOutOfRange_Returns400(int limit)
    {
        var response = await Client.GetTestAsync($"/api/shortpositions/top?limit={limit}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Company_LowercaseWithSt_ReturnsData()
    {
        var id = TestId();
        await SeedAsync($"LEICO{id}", $"Co {id}", $"CO{id}", 5.5m);

        var response = await Client.GetTestAsync($"/api/shortpositions/company?symbol=co{id.ToLower()}.st");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonTestAsync<ShortCompanyDto>();
        dto!.Lei.Should().Be($"LEICO{id}");
        dto.PositionPercent.Should().Be(5.5m);
    }

    [Fact]
    public async Task Company_Unknown_Returns404()
    {
        var response = await Client.GetTestAsync($"/api/shortpositions/company?symbol=NOPE{TestId()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Company_EmptySymbol_Returns400()
    {
        var response = await Client.GetTestAsync("/api/shortpositions/company?symbol=");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
