using System.Globalization;
using System.Text.RegularExpressions;
using AktieKoll.Models;

namespace AktieKoll.Services;

public static class FiShortFileParser
{
    private const int MinAggregateRows = 50;
    private const int HeaderScanRows = 10;

    private static readonly CultureInfo Sv = CultureInfo.GetCultureInfo("sv-SE");

    private static readonly string[] AggregateHeader =
        ["Namn på emittent", "LEI", "Position i procent", "Positionsdatum"];

    private static readonly string[] PositionHeader =
        ["Innehavare av positionen", "Namn på emittent", "ISIN", "Position i procent", "Datum för positionen"];

    public static IReadOnlyList<FiAggregateRow> ParseAggregate(Stream ods)
    {
        var rows = OdsReader.ReadFirstSheet(ods);
        int start = FindHeader(rows, AggregateHeader, "aggregate");

        var result = new List<FiAggregateRow>();
        for (int i = start; i < rows.Count; i++)
        {
            var r = rows[i];
            var lei = Cell(r, 1);
            if (lei.Length == 0) continue;
            if (!TryParsePercent(Cell(r, 2), out var pct, out _)) continue;
            if (!TryParseDate(Cell(r, 3), out var date)) continue;
            result.Add(new FiAggregateRow(NormalizeName(Cell(r, 0)), lei, pct, date));
        }

        if (result.Count < MinAggregateRows)
            throw new FiFormatException(
                $"FI aggregate file parsed only {result.Count} rows (expected at least {MinAggregateRows}); format may have changed.");
        return result;
    }

    public static IReadOnlyList<FiPositionRow> ParsePositions(Stream ods)
    {
        var rows = OdsReader.ReadFirstSheet(ods);
        int start = FindHeader(rows, PositionHeader, "position");

        var result = new List<FiPositionRow>();
        for (int i = start; i < rows.Count; i++)
        {
            var r = rows[i];
            var isin = Cell(r, 2);
            if (isin.Length == 0) continue;
            if (!TryParsePercent(Cell(r, 3), out var pct, out var below)) continue;
            if (!TryParseDate(Cell(r, 4), out var date)) continue;
            var comment = Cell(r, 5);
            result.Add(new FiPositionRow(
                NormalizeName(Cell(r, 0)), NormalizeName(Cell(r, 1)), isin, pct, below, date,
                comment.Length == 0 ? null : comment));
        }

        if (result.Count == 0)
            throw new FiFormatException("FI position file parsed 0 rows; format may have changed.");
        return result;
    }

    public static string NormalizeName(string name)
        => Regex.Replace(name, @"[\s ]+", " ").Trim(' ', ' ');

    private static string Cell(List<string> row, int index)
        => index < row.Count ? row[index].Trim().Trim(' ').Trim() : "";

    private static int FindHeader(List<List<string>> rows, string[] expected, string kind)
    {
        int limit = Math.Min(HeaderScanRows, rows.Count);
        for (int i = 0; i < limit; i++)
        {
            var r = rows[i];
            if (r.Count < expected.Length) continue;
            bool match = true;
            for (int c = 0; c < expected.Length; c++)
            {
                if (!Cell(r, c).StartsWith(expected[c], StringComparison.OrdinalIgnoreCase))
                {
                    match = false;
                    break;
                }
            }
            if (match) return i + 1;
        }
        throw new FiFormatException(
            $"FI {kind} file: expected header [{string.Join(", ", expected)}] not found in the first {HeaderScanRows} rows.");
    }

    private static bool TryParsePercent(string value, out decimal percent, out bool belowThreshold)
    {
        percent = 0;
        belowThreshold = false;
        value = value.Trim();
        if (value == "<0,5")
        {
            belowThreshold = true;
            return true;
        }
        return decimal.TryParse(value, NumberStyles.Number, Sv, out percent);
    }

    private static bool TryParseDate(string value, out DateTime date)
        => DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
