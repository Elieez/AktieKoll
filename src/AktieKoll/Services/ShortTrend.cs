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

    /// <summary>
    /// A holder's position in an issuer is one stream: FI rows may carry either share class's ISIN
    /// (an exit can be filed under the other class) and the same holder appears with differing casing.
    /// </summary>
    public static string HolderKey(string holderName) => holderName.Trim().ToUpperInvariant();

    /// <param name="fileKnown">
    /// Whether ingestion has marked rows in FI's current file. Null = infer from the events.
    /// When true, streams with a positive last event that are absent from the file are "stale"
    /// (the filer was renamed or the exit was never filed under that name): they are closed at the
    /// earliest unclaimed orphan exit (a below-threshold event from a stream that never had a
    /// positive event), oldest stale stream first, else dropped at their own last event.
    /// </param>
    public static IReadOnlyList<SeriesPointDto> SignificantSeries(
        IEnumerable<ShortPosition> events, DateTime today, bool? fileKnown = null)
    {
        var all = events.OrderBy(e => e.PositionDate).ThenBy(e => e.Id).ToList();
        var known = fileKnown ?? all.Any(e => e.InCurrentFile);

        // value overrides keyed by event id, plus synthetic closing events (key, date, 0)
        var overrides = new Dictionary<int, decimal>();
        var closings = new List<(string Key, DateTime Date)>();

        if (known)
        {
            var streams = all.GroupBy(e => HolderKey(e.HolderName)).ToList();
            static bool Positive(ShortPosition e) => !e.BelowThreshold && e.PositionPercent > 0;

            var stale = streams
                .Where(g => !g.Any(e => e.InCurrentFile) && Positive(g.Last()))
                .Select(g => (Key: g.Key, Last: g.Last()))
                .OrderBy(s => s.Last.PositionDate).ThenBy(s => s.Last.Id)
                .ToList();
            var orphanExits = streams
                .Where(g => !g.Any(Positive))
                .SelectMany(g => g.Where(e => e.BelowThreshold))
                .OrderBy(e => e.PositionDate).ThenBy(e => e.Id)
                .ToList();

            var used = new HashSet<int>();
            foreach (var s in stale)
            {
                var exit = orphanExits.FirstOrDefault(x => !used.Contains(x.Id) && x.PositionDate >= s.Last.PositionDate);
                if (exit is null) overrides[s.Last.Id] = 0m;
                else { used.Add(exit.Id); closings.Add((s.Key, exit.PositionDate)); }
            }
        }

        var latest = new Dictionary<string, decimal>();
        var points = new List<SeriesPointDto>();
        decimal? last = null;

        foreach (var day in all.Select(e => e.PositionDate).Concat(closings.Select(c => c.Date)).Distinct().Order())
        {
            foreach (var e in all.Where(e => e.PositionDate == day))
                latest[HolderKey(e.HolderName)] = e.BelowThreshold ? 0m : overrides.GetValueOrDefault(e.Id, e.PositionPercent);
            foreach (var c in closings.Where(c => c.Date == day))
                latest[c.Key] = 0m;

            var sum = latest.Values.Sum();
            if (sum != last)
            {
                points.Add(new SeriesPointDto(day, sum));
                last = sum;
            }
        }

        if (points.Count == 0 || points[^1].Date != today)
            points.Add(new SeriesPointDto(today, last ?? 0m));
        return points;
    }
}
