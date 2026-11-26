using AktieKoll.Models;
using AktieKoll.Services;
using AktieKoll.Tests.Shared.TestHelpers;
using FluentAssertions;

namespace AktieKoll.Tests.Unit;

public class FiShortFileParserTests
{
    private static readonly string[] AggHeader =
    [
        "Namn på emittent (Name of the issuer)", "LEI",
        "Position i procent(Position in per cent)",
        "Positionsdatum senaste position (Position date, latest position)"
    ];

    private static readonly string[] PosHeader =
    [
        "Innehavare av positionen (Position holder)", "Namn på emittent (Name of the issuer)", "ISIN",
        "Position i procent(Position in per cent)", "Datum för positionen (Position date)", "Kommentar(Comment)"
    ];

    private static List<string[]> AggFile(int generated)
    {
        var rows = new List<string[]>
        {
            new[] { "Aggregerade positioner" },
            new[] { "Aggregerade korta nettopositioner i aktier (blankning)", "", "", "fi.se/blankning" },
            new[] { "", "", "", "rapportering@fi.se" },
            AggHeader,
            new[] { " Orexo AB", "549300LJ5CCWDPTK9Z08", "6,03", "2026-11-26" }
        };
        for (int i = 0; i < generated; i++)
            rows.Add([$"Bolag {i}", $"LEI{i:D17}", "1,25", "2026-11-25"]);
        return rows;
    }

    private static List<string[]> PosFile(params string[][] data)
    {
        var rows = new List<string[]>
        {
            new[] { "Aktuella positioner" },
            new[] { "Korta positioner", "", "", "", "", "fi.se/blankning" },
            new[] { "" },
            PosHeader
        };
        rows.AddRange(data);
        return rows;
    }

    [Fact]
    public void ParseAggregate_RealLayout_ParsesRows()
    {
        using var ods = OdsTestBuilder.Build(AggFile(60));

        var result = FiShortFileParser.ParseAggregate(ods);

        result.Should().HaveCount(61);
        var orexo = result[0];
        orexo.IssuerName.Should().Be("Orexo AB");
        orexo.Lei.Should().Be("549300LJ5CCWDPTK9Z08");
        orexo.PositionPercent.Should().Be(6.03m);
        orexo.LatestPositionDate.Should().Be(new DateTime(2026, 11, 26));
    }

    [Fact]
    public void ParseAggregate_FewerThan50Rows_Throws()
    {
        using var ods = OdsTestBuilder.Build(AggFile(9));

        var act = () => FiShortFileParser.ParseAggregate(ods);

        act.Should().Throw<FiFormatException>();
    }

    [Fact]
    public void ParseAggregate_HeaderChanged_Throws()
    {
        var rows = AggFile(60);
        rows[3] = ["Issuer", "LEI", "Position i procent", "Positionsdatum"];
        using var ods = OdsTestBuilder.Build(rows);

        var act = () => FiShortFileParser.ParseAggregate(ods);

        act.Should().Throw<FiFormatException>().WithMessage("*expected header*");
    }

    [Fact]
    public void ParsePositions_BelowThreshold_IsZeroAndFlagged()
    {
        using var ods = OdsTestBuilder.Build(PosFile(
            ["Fond AB", "Orexo AB", "SE0000000001", "<0,5", "2026-01-02", ""],
            ["Fond B", "Orexo AB", "SE0000000001", "0,75", "2026-01-03", "kommentar"]));

        var result = FiShortFileParser.ParsePositions(ods);

        result.Should().HaveCount(2);
        result[0].PositionPercent.Should().Be(0m);
        result[0].BelowThreshold.Should().BeTrue();
        result[0].Comment.Should().BeNull();
        result[1].PositionPercent.Should().Be(0.75m);
        result[1].BelowThreshold.Should().BeFalse();
        result[1].Comment.Should().Be("kommentar");
        result[1].PositionDate.Should().Be(new DateTime(2026, 1, 3));
    }

    [Fact]
    public void ParsePositions_NameWithTrailingNbsp_IsTrimmed()
    {
        using var ods = OdsTestBuilder.Build(PosFile(
            ["Fond AB", "Elekta AB (publ) ", "SE0000000002", "1,10", "2026-01-02", ""]));

        var result = FiShortFileParser.ParsePositions(ods);

        result.Should().ContainSingle().Which.IssuerName.Should().Be("Elekta AB (publ)");
    }

    [Fact]
    public void ParsePositions_RepeatedEmptyCellsAndRows_Ignored()
    {
        var rows = PosFile(["Fond AB", "Orexo AB", "SE0000000001", "1,00", "2026-01-02", ""]);
        using var ods = OdsTestBuilder.Build(rows);

        var sheet = OdsReader.ReadFirstSheet(ods);
        ods.Position = 0;
        var result = FiShortFileParser.ParsePositions(ods);

        sheet.Should().HaveCount(4); // blank row skipped
        sheet.All(r => r.Count < 10).Should().BeTrue();
        result.Should().HaveCount(1);
    }

    [Fact]
    public void ParsePositions_SkipsRowsWithEmptyIsinOrBadValues_AndThrowsWhenNone()
    {
        using var ods = OdsTestBuilder.Build(PosFile(
            ["Fond AB", "Orexo AB", "", "1,00", "2026-01-02", ""],
            ["Fond AB", "Orexo AB", "SE0000000001", "abc", "2026-01-02", ""],
            ["Fond AB", "Orexo AB", "SE0000000001", "1,00", "not-a-date", ""]));

        var act = () => FiShortFileParser.ParsePositions(ods);

        act.Should().Throw<FiFormatException>();
    }

    [Fact]
    public void NormalizeName_TrimsAndCollapses()
    {
        FiShortFileParser.NormalizeName("  Foo   Bar  ").Should().Be("Foo Bar");
    }
}
