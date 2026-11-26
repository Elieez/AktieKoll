using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using AktieKoll.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AktieKoll.Tests.Shared.TestHelpers.ServiceTestHelpers;

namespace AktieKoll.Tests.Unit;

public class ShortIssuerResolverTests
{
    private const string Lei = "549300ABCDEFGHIJKL12";
    private static readonly DateTime Now = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

    private readonly ApplicationDbContext _db = CreateContext();
    private readonly Mock<IGleifClient> _gleif = new();
    private readonly Mock<ISymbolService> _symbols = new();
    private readonly Mock<TimeProvider> _time = new();

    public ShortIssuerResolverTests()
    {
        _time.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(Now));
        _gleif.Setup(g => g.GetIsinsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
    }

    private ShortIssuerResolver Create()
        => new(_db, _gleif.Object, _symbols.Object, _time.Object, NullLogger<ShortIssuerResolver>.Instance);

    private static FiAggregateRow Agg(string name, string lei = Lei)
        => new(name, lei, 1.5m, new DateTime(2026, 11, 20));

    private static FiPositionRow Pos(string issuer, string isin)
        => new("Holder", issuer, isin, 0.6m, false, new DateTime(2026, 11, 20), null);

    private static InsiderTrade Trade(string symbol, string? lei, string? isin, DateTime? published = null) => new()
    {
        CompanyName = "X", InsiderName = "Y", TransactionType = "Förvärv", Shares = 1, Price = 1,
        Currency = "SEK", Status = "Ok", Lei = lei, Isin = isin, Symbol = symbol,
        PublishingDate = published ?? Now.AddDays(-5), TransactionDate = (published ?? Now.AddDays(-5)).Date
    };

    [Fact]
    public async Task Resolve_NameMatch_UsesPositionIsinAndCompanyTicker()
    {
        await SeedCompanies(_db, ("SE0009554454", "SBB-B"));
        const string name = "Samhällsbyggnadsbolaget i Norden AB";

        var map = await Create().ResolveAsync([Agg(name)], [Pos(name, "SE0009554454")], Ct);

        var issuer = await _db.ShortIssuers.SingleAsync(Ct);
        issuer.PrimarySymbol.Should().Be("SBB-B");
        map.LeiByIsin["SE0009554454"].Should().Be(Lei);
        _gleif.Verify(g => g.GetIsinsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Resolve_NameWithNbspAndCase_MatchesIssuer()
    {
        await SeedCompanies(_db, ("SE0000000001", "NOTE"));

        var map = await Create().ResolveAsync([Agg(" NOTE AB (publ)")], [Pos("Note AB (publ) ", "SE0000000001")], Ct);

        map.LeiByIsin["SE0000000001"].Should().Be(Lei);
        map.LeiByName["note ab (publ)"].Should().Be(Lei);
        (await _db.ShortIssuers.SingleAsync(Ct)).PrimarySymbol.Should().Be("NOTE");
    }

    [Fact]
    public async Task Resolve_ShareClasses_AllTickersMapped_PrimaryIsMostTraded()
    {
        await SeedCompanies(_db, ("SE0000115420", "VOLV-A"), ("SE0000115446", "VOLV-B"));
        _db.InsiderTrades.AddRange(
            Trade("VOLV-B", Lei, null), Trade("VOLV-B", Lei, null), Trade("VOLV-B", Lei, null),
            Trade("VOLV-A", Lei, null));
        await _db.SaveChangesAsync(Ct);

        await Create().ResolveAsync([Agg("Volvo AB")],
            [Pos("Volvo AB", "SE0000115420"), Pos("Volvo AB", "SE0000115446")], Ct);

        var symbols = await _db.ShortIssuerSymbols.Select(s => s.Symbol).ToListAsync(Ct);
        symbols.Should().BeEquivalentTo(["VOLV-A", "VOLV-B"]);
        (await _db.ShortIssuers.SingleAsync(Ct)).PrimarySymbol.Should().Be("VOLV-B");
    }

    [Fact]
    public async Task Resolve_ShareClassesNoTrades_PrimaryPrefersB()
    {
        await SeedCompanies(_db, ("SE0000000011", "SSAB-A"), ("SE0000000012", "SSAB-B"));

        await Create().ResolveAsync([Agg("SSAB AB")],
            [Pos("SSAB AB", "SE0000000011"), Pos("SSAB AB", "SE0000000012")], Ct);

        (await _db.ShortIssuers.SingleAsync(Ct)).PrimarySymbol.Should().Be("SSAB-B");
    }

    [Fact]
    public async Task Resolve_InsiderTradeLei_AddsIsinAndSymbol()
    {
        _db.InsiderTrades.Add(Trade("ABC", Lei, "SE0000000002"));
        await _db.SaveChangesAsync(Ct);

        var map = await Create().ResolveAsync([Agg("Abc AB")], [], Ct);

        map.LeiByIsin["SE0000000002"].Should().Be(Lei);
        (await _db.ShortIssuerSymbols.SingleAsync(Ct)).Symbol.Should().Be("ABC");
        (await _db.ShortIssuers.SingleAsync(Ct)).PrimarySymbol.Should().Be("ABC");
    }

    [Fact]
    public async Task Resolve_GleifFallback_OnlyWhenNoIsins_AndThrottled7Days()
    {
        await SeedCompanies(_db, ("SE0000000003", "GLF"));
        _gleif.Setup(g => g.GetIsinsAsync(Lei, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "SE0000000003", "XS9999999999" });

        var map = await Create().ResolveAsync([Agg("Gleif AB")], [], Ct);
        map.LeiByIsin.Keys.Should().BeEquivalentTo(["SE0000000003"]);
        (await _db.ShortIssuers.SingleAsync(Ct)).GleifCheckedAt.Should().Be(Now);
        _gleif.Verify(g => g.GetIsinsAsync(Lei, It.IsAny<CancellationToken>()), Times.Once);

        // 3 days later: throttled.
        _time.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(Now.AddDays(3)));
        await Create().ResolveAsync([Agg("Gleif AB")], [], Ct);
        _gleif.Verify(g => g.GetIsinsAsync(Lei, It.IsAny<CancellationToken>()), Times.Once);

        // 8 days later: queried again.
        _time.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(Now.AddDays(8)));
        await Create().ResolveAsync([Agg("Gleif AB")], [], Ct);
        _gleif.Verify(g => g.GetIsinsAsync(Lei, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Resolve_Unresolved_KeepsIssuerWithNullSymbol()
    {
        _symbols.Setup(s => s.ResolveSymbolAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync((string?)null);

        var map = await Create().ResolveAsync([Agg("Unknown AB")], [], Ct);

        var issuer = await _db.ShortIssuers.SingleAsync(Ct);
        issuer.PrimarySymbol.Should().BeNull();
        issuer.Name.Should().Be("Unknown AB");
        (await _db.ShortIssuerSymbols.CountAsync(Ct)).Should().Be(0);
        map.LeiByName["unknown ab"].Should().Be(Lei);
    }
}
