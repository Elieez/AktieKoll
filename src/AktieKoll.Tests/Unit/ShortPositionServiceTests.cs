using AktieKoll.Data;
using AktieKoll.Dtos;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using AktieKoll.Services;
using FluentAssertions;
using Moq;
using static AktieKoll.Tests.Shared.TestHelpers.ServiceTestHelpers;

namespace AktieKoll.Tests.Unit;

public class ShortPositionServiceTests
{
    private readonly ApplicationDbContext _db = CreateContext();
    private readonly Mock<TimeProvider> _time = new();
    private static readonly DateTime Today = new(2026, 11, 26);

    public ShortPositionServiceTests()
        => _time.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero));

    private ShortPositionService Create() => new(_db, _time.Object);

    private void Issuer(string lei, string name, string primary, params string[] symbols)
    {
        _db.ShortIssuers.Add(new ShortIssuer { Lei = lei, Name = name, PrimarySymbol = primary });
        foreach (var s in symbols) _db.ShortIssuerSymbols.Add(new ShortIssuerSymbol { Lei = lei, Symbol = s });
    }

    private void Snap(string lei, DateTime date, decimal pct)
        => _db.ShortSnapshots.Add(new ShortSnapshot { Lei = lei, SnapshotDate = date, PositionPercent = pct, LatestPositionDate = date });

    private void Holder(string lei, string holder, decimal pct, DateTime date, bool below = false, string isin = "SE0000000001")
        => _db.ShortPositions.Add(new ShortPosition
        {
            Lei = lei, HolderName = holder, IssuerName = "X", Isin = isin,
            PositionPercent = pct, BelowThreshold = below, PositionDate = date,
        });

    private void Trade(string symbol, string type, int daysAgo)
        => _db.InsiderTrades.Add(new InsiderTrade
        {
            CompanyName = "X", InsiderName = "I", TransactionType = type, Shares = 1, Price = 1, Currency = "SEK",
            Status = "Aktuell", Symbol = symbol, PublishingDate = Today.AddDays(-daysAgo), TransactionDate = Today.AddDays(-daysAgo),
        });

    [Fact]
    public async Task GetTop_Highest_ExcludesZeroAndOrdersDesc()
    {
        Issuer("L1", "One", "ONE.ST", "ONE.ST"); Issuer("L2", "Two", "TWO.ST", "TWO.ST"); Issuer("L3", "Three", "THR.ST", "THR.ST");
        Snap("L1", Today, 1.0m); Snap("L2", Today, 3.0m); Snap("L3", Today, 0m);
        await _db.SaveChangesAsync(Ct);

        var result = await Create().GetTopAsync(ShortSort.Highest, 30, 10, Ct);

        result.Items.Select(i => i.Lei).Should().Equal("L2", "L1");
        result.UpdatedAt.Should().Be(Today);
    }

    [Fact]
    public async Task GetTop_Decrease_IncludesDropOut()
    {
        Issuer("L1", "One", "ONE.ST", "ONE.ST"); Issuer("L2", "Two", "TWO.ST", "TWO.ST");
        Snap("L1", Today.AddDays(-30), 2.0m); Snap("L1", Today, 0m);
        Snap("L2", Today.AddDays(-30), 2.0m); Snap("L2", Today, 1.5m);
        await _db.SaveChangesAsync(Ct);

        var result = await Create().GetTopAsync(ShortSort.Decrease, 30, 10, Ct);

        result.Items.Select(i => (i.Lei, i.Change)).Should().Equal(("L1", -2.0m), ("L2", -0.5m));
    }

    [Fact]
    public async Task GetTop_InsiderCounts_SumAcrossShareClasses()
    {
        Issuer("L1", "Volvo", "VOLV-B.ST", "VOLV-A.ST", "VOLV-B.ST");
        Snap("L1", Today, 1.0m);
        Trade("VOLV-A.ST", "Förvärv", 5); Trade("VOLV-B.ST", "FÖRVÄRV", 10); Trade("VOLV-B.ST", "Avyttring", 20);
        Trade("VOLV-B.ST", "Förvärv", 120); Trade("OTHER.ST", "Förvärv", 1);
        await _db.SaveChangesAsync(Ct);

        var item = (await Create().GetTopAsync(ShortSort.Highest, 30, 10, Ct)).Items.Single();

        item.InsiderBuys90d.Should().Be(2);
        item.InsiderSells90d.Should().Be(1);
    }

    [Fact]
    public async Task GetCompany_AnyShareClassTicker_ReturnsIssuer()
    {
        Issuer("L1", "Volvo", "VOLV-B.ST", "VOLV-A.ST", "VOLV-B.ST");
        Snap("L1", Today.AddDays(-1), 1.0m); Snap("L1", Today, 1.1m);
        Holder("L1", "Fund", 0.8m, Today.AddDays(-3));
        await _db.SaveChangesAsync(Ct);

        var a = await Create().GetCompanyAsync("volv-a.st", Ct);
        var b = await Create().GetCompanyAsync("VOLV-B", Ct);

        a!.Lei.Should().Be("L1");
        b!.Lei.Should().Be("L1");
        a.Symbol.Should().Be("VOLV-B.ST");
        a.TotalSeries.Should().HaveCount(2);
        a.SignificantSeries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetCompany_Unknown_ReturnsNull()
    {
        Issuer("L1", "Volvo", "VOLV-B.ST", "VOLV-B.ST"); // no snapshot
        await _db.SaveChangesAsync(Ct);

        (await Create().GetCompanyAsync("NOPE", Ct)).Should().BeNull();
        (await Create().GetCompanyAsync("VOLV-B", Ct)).Should().BeNull();
    }

    [Fact]
    public async Task GetCompany_Holders_LatestPerHolderOnly()
    {
        Issuer("L1", "Volvo", "VOLV-B.ST", "VOLV-B.ST");
        Snap("L1", Today, 1.0m);
        Holder("L1", "A", 0.9m, new DateTime(2026, 10, 1));
        Holder("L1", "A", 0.5m, new DateTime(2026, 11, 1), below: true);
        Holder("L1", "B", 0.6m, new DateTime(2026, 11, 2));
        Holder("L1", "C", 1.2m, new DateTime(2026, 11, 3));
        await _db.SaveChangesAsync(Ct);

        var company = (await Create().GetCompanyAsync("VOLV-B", Ct))!;

        company.Holders.Select(h => h.HolderName).Should().Equal("C", "B");
        var top = await Create().GetTopAsync(ShortSort.Highest, 30, 10, Ct);
        top.Items.Single().HolderCount.Should().Be(2);
    }

    [Fact]
    public async Task GetTop_Increase_OrdersByChangeDescAndExcludesDecreases()
    {
        Issuer("L1", "One", "ONE.ST", "ONE.ST"); Issuer("L2", "Two", "TWO.ST", "TWO.ST"); Issuer("L3", "Three", "THR.ST", "THR.ST");
        Snap("L1", Today.AddDays(-30), 1.0m); Snap("L1", Today, 1.5m);
        Snap("L2", Today.AddDays(-30), 1.0m); Snap("L2", Today, 3.0m);
        Snap("L3", Today.AddDays(-30), 2.0m); Snap("L3", Today, 1.0m);
        await _db.SaveChangesAsync(Ct);

        var result = await Create().GetTopAsync(ShortSort.Increase, 30, 10, Ct);

        result.Items.Select(i => (i.Lei, i.Change)).Should().Equal(("L2", 2.0m), ("L1", 0.5m));
    }

    [Fact]
    public async Task GetTop_DropOutOlderThanWindow_KeepsBaselineAsCurrentAndIsExcluded()
    {
        Issuer("L1", "One", "ONE.ST", "ONE.ST"); Issuer("L2", "Two", "TWO.ST", "TWO.ST");
        Snap("L1", Today.AddDays(-90), 2.0m); Snap("L1", Today.AddDays(-60), 0m);
        Snap("L2", Today, 1.0m);
        await _db.SaveChangesAsync(Ct);

        var result = await Create().GetTopAsync(ShortSort.Highest, 30, 10, Ct);

        result.Items.Select(i => i.Lei).Should().Equal("L2");
    }

    [Fact]
    public async Task GetCompany_HolderOnTwoShareClasses_BothCounted()
    {
        Issuer("L1", "Volvo", "VOLV-B.ST", "VOLV-B.ST");
        Snap("L1", Today, 1.0m);
        Holder("L1", "Fund", 0.6m, new DateTime(2026, 11, 1), isin: "SE0000000001");
        Holder("L1", "Fund", 0.7m, new DateTime(2026, 11, 2), isin: "SE0000000002");
        await _db.SaveChangesAsync(Ct);

        var company = (await Create().GetCompanyAsync("VOLV-B", Ct))!;

        company.Holders.Should().HaveCount(2);
        company.SignificantSeries.Last().Value.Should().Be(1.3m);
        (await Create().GetTopAsync(ShortSort.Highest, 30, 10, Ct)).Items.Single().HolderCount.Should().Be(2);
    }

    [Fact]
    public void InsiderCutoff_IsUtcKind()
    {
        var cutoff = ShortPositionService.InsiderCutoff(_time.Object);

        cutoff.Kind.Should().Be(DateTimeKind.Utc);
        cutoff.Should().Be(new DateTime(2026, 8, 28, 0, 0, 0, DateTimeKind.Utc));
    }
}
