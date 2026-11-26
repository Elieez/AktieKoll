using AktieKoll.Dtos;
using AktieKoll.Models;

namespace AktieKoll.Services;

public static class ShortTrend
{
    public static (decimal? Change, DateTime? ChangeSince) ComputeChange(
        IReadOnlyList<SeriesPointDto> snapshotsAsc, DateTime today, int days)
    {
        if (snapshotsAsc.Count < 2) return (null, null);

        var current = snapshotsAsc[^1];
        var windowStart = today.AddDays(-days);

        SeriesPointDto? baseline = null;
        for (var i = snapshotsAsc.Count - 1; i >= 0; i--)
        {
            if (snapshotsAsc[i].Date <= windowStart) { baseline = snapshotsAsc[i]; break; }
        }

        if (baseline is not null)
        {
            if (ReferenceEquals(baseline, current)) return (null, null);
            return (current.Value - baseline.Value, null);
        }

        var oldest = snapshotsAsc[0];
        return (current.Value - oldest.Value, oldest.Date);
    }

    public static IReadOnlyList<SeriesPointDto> SignificantSeries(IEnumerable<ShortPosition> events, DateTime today)
    {
        var latest = new Dictionary<(string, string), decimal>();
        var points = new List<SeriesPointDto>();
        decimal? last = null;

        foreach (var day in events.OrderBy(e => e.PositionDate).ThenBy(e => e.Id).GroupBy(e => e.PositionDate))
        {
            foreach (var e in day)
                latest[(e.HolderName, e.Isin)] = e.BelowThreshold ? 0m : e.PositionPercent;

            var sum = latest.Values.Sum();
            if (sum != last)
            {
                points.Add(new SeriesPointDto(day.Key, sum));
                last = sum;
            }
        }

        if (points.Count == 0 || points[^1].Date != today)
            points.Add(new SeriesPointDto(today, last ?? 0m));
        return points;
    }
}
