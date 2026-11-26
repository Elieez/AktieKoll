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

public class ShortIngestionServiceTests
{
    private readonly ApplicationDbContext _db = CreateContext();
    private readonly Mock<TimeProvider> _time = new();

    public ShortIngestionServiceTests()
    {
        SetNow(new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero));
    }

    private void SetNow(DateTimeOffset now) => _time.Setup(t => t.GetUtcNow()).Returns(now);

    private ShortIngestionService Create()
    {
        var gleif = new Mock<IGleifClient>();
        gleif.Setup(g => g.GetIsinsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        var symbols = new Mock<ISymbolService>();
        var resolver = new ShortIssuerResolver(_db, gleif.Object, symbols.Object, _time.Object,
            NullLogger<ShortIssuerResolver>.Instance);
        return new ShortIngestionService(_db, resolver, _time.Object, NullLogger<ShortIngestionService>.Instance);
    }

    private static FiAggregateRow Agg(string name, string lei, decimal pct = 1.2m)
        => new(name, lei, pct, new DateTime(2026, 11, 20));

    private static FiPositionRow Pos(string holder, string issuer, string isin, int day = 20)
        => new(holder, issuer, isin, 0.6m, false, new DateTime(2026, 11, day), null);

    [Fact]
    public async Task Ingest_FirstRun_ImportsHistoryAndWritesSnapshot()
    {
        var agg = new[] { Agg("A AB", "LEI_A"), Agg("B AB", "LEI_B"), Agg("C AB", "LEI_C") };
        var current = new[] { Pos("H1", "A AB", "SE0000000001"), Pos("H2", "B AB", "SE0000000002") };
        var history = new[]
        {
            Pos("H1", "A AB", "SE0000000001"), // overlaps current
            Pos("H1", "A AB", "SE0000000001", 10),
            Pos("H2", "B AB", "SE0000000002", 10),
            Pos("H3", "C AB", "SE0000000003", 10),
            Pos("H3", "C AB", "SE0000000003", 11),
        };

        var result = await Create().IngestAsync(agg, current, history, Ct);

        result.NewPositions.Should().Be(6);
        result.Snapshots.Should().Be(3);
        (await _db.ShortPositions.CountAsync(Ct)).Should().Be(6);
        (await _db.ShortPositions.Where(p => p.Lei == null).CountAsync(Ct)).Should().Be(0);
        (await _db.ShortSnapshots.CountAsync(Ct)).Should().Be(3);
    }

    [Fact]
    public async Task Ingest_DuplicatePositionRows_InsertedOnce()
    {
        var row = Pos("H1", "A AB", "SE0000000001");

        var result = await Create().IngestAsync([Agg("A AB", "LEI_A")], [row, row with { PositionPercent = 9m }], [row], Ct);

        result.NewPositions.Should().Be(1);
        (await _db.ShortPositions.SingleAsync(Ct)).PositionPercent.Should().Be(0.6m);
    }

    [Fact]
    public async Task Ingest_RerunSameDay_IsIdempotent()
    {
        // X was shorted yesterday, absent today -> one zero row, which must not be duplicated on rerun.
        _db.ShortIssuers.Add(new ShortIssuer { Lei = "LEI_X", Name = "X AB" });
        _db.ShortSnapshots.Add(new ShortSnapshot { Lei = "LEI_X", SnapshotDate = new DateTime(2026, 11, 25), PositionPercent = 1.2m });
        await _db.SaveChangesAsync(Ct);
        var agg = new[] { Agg("A AB", "LEI_A") };
        var current = new[] { Pos("H1", "A AB", "SE0000000001") };

        var first = await Create().IngestAsync(agg, current, [], Ct);
        var second = await Create().IngestAsync(agg, current, [], Ct);

        first.NewPositions.Should().Be(1);
        first.DroppedOut.Should().Be(1);
        second.NewPositions.Should().Be(0);
        second.DroppedOut.Should().Be(1);
        var today = new DateTime(2026, 11, 26);
        (await _db.ShortSnapshots.CountAsync(s => s.SnapshotDate == today, Ct)).Should().Be(2);
        (await _db.ShortSnapshots.CountAsync(s => s.SnapshotDate == today && s.Lei == "LEI_X", Ct)).Should().Be(1);
    }

    [Fact]
    public async Task Ingest_IssuerDropsOut_WritesOneZeroRowThenNothing()
    {
        var withX = new[] { Agg("X AB", "LEI_X"), Agg("A AB", "LEI_A") };
        var withoutX = new[] { Agg("A AB", "LEI_A") };

        SetNow(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
        await Create().IngestAsync(withX, [], [], Ct);
        SetNow(new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero));
        var day2 = await Create().IngestAsync(withoutX, [], [], Ct);
        SetNow(new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero));
        var day3 = await Create().IngestAsync(withoutX, [], [], Ct);

        day2.DroppedOut.Should().Be(1);
        day3.DroppedOut.Should().Be(0);
        var zero = await _db.ShortSnapshots.SingleAsync(s => s.Lei == "LEI_X" && s.PositionPercent == 0, Ct);
        zero.SnapshotDate.Should().Be(new DateTime(2026, 11, 25));
        zero.LatestPositionDate.Should().BeNull();
        (await _db.ShortSnapshots.CountAsync(s => s.Lei == "LEI_X", Ct)).Should().Be(2);
    }

    [Fact]
    public async Task Ingest_EmptyAggregate_ThrowsAndWritesNothing()
    {
        _db.ShortIssuers.Add(new ShortIssuer { Lei = "LEI_X", Name = "X AB" });
        _db.ShortSnapshots.Add(new ShortSnapshot { Lei = "LEI_X", SnapshotDate = new DateTime(2026, 11, 25), PositionPercent = 1.2m });
        await _db.SaveChangesAsync(Ct);

        var act = () => Create().IngestAsync([], [], [], Ct);

        await act.Should().ThrowAsync<ArgumentException>();
        (await _db.ShortSnapshots.CountAsync(Ct)).Should().Be(1);
    }

    [Fact]
    public async Task Ingest_SnapshotDate_IsStockholmDate()
    {
        SetNow(new DateTimeOffset(2026, 11, 26, 23, 30, 0, TimeSpan.Zero));

        await Create().IngestAsync([Agg("A AB", "LEI_A")], [], [], Ct);

        (await _db.ShortSnapshots.SingleAsync(Ct)).SnapshotDate.Should().Be(new DateTime(2026, 11, 27));
    }

    [Fact]
    public async Task Ingest_LaterResolution_BackfillsPositionLei()
    {
        _db.ShortPositions.Add(new ShortPosition
        {
            HolderName = "H1", IssuerName = "Renamed AB", Isin = "SE0000000001",
            PositionPercent = 0.6m, PositionDate = new DateTime(2026, 11, 20), Lei = null
        });
        await _db.SaveChangesAsync(Ct);

        // The ISIN becomes known through another position row of the same issuer name.
        var result = await Create().IngestAsync([Agg("A AB", "LEI_A")], [Pos("H2", "A AB", "SE0000000001")], [], Ct);

        result.NewPositions.Should().Be(1);
        (await _db.ShortPositions.Where(p => p.Lei == null).CountAsync(Ct)).Should().Be(0);
        (await _db.ShortPositions.SingleAsync(p => p.HolderName == "H1", Ct)).Lei.Should().Be("LEI_A");
    }
}
