using AktieKoll.Data;
using AktieKoll.Dtos;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Services;

public class ShortPositionService(ApplicationDbContext db, TimeProvider time) : IShortPositionService
{
    private const int InsiderWindowDays = 90;

    private static readonly TimeZoneInfo Stockholm = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    private DateTime Today => TimeZoneInfo.ConvertTime(time.GetUtcNow(), Stockholm).Date;

    // Kind=Utc so Npgsql accepts it against timestamptz columns.
    internal static DateTime InsiderCutoff(TimeProvider time) =>
        time.GetUtcNow().UtcDateTime.Date.AddDays(-InsiderWindowDays);

    private sealed record Snap(string Lei, DateTime Date, decimal Pct, DateTime? LatestPos);

    private sealed record HolderEvent(string Lei, string HolderName, string Isin, decimal Pct, bool Below, DateTime Date, int Id, bool InFile);

    public async Task<ShortTopListDto> GetTopAsync(ShortSort sort, int days, int limit, CancellationToken ct)
    {
        var today = Today;
        var windowStart = today.AddDays(-days);

        var updatedAt = await db.ShortSnapshots.AsNoTracking()
            .MaxAsync(s => (DateTime?)s.SnapshotDate, ct);

        var windowRows = await db.ShortSnapshots.AsNoTracking()
            .Where(s => s.SnapshotDate > windowStart)
            .Select(s => new Snap(s.Lei, s.SnapshotDate, s.PositionPercent, s.LatestPositionDate))
            .ToListAsync(ct);

        var baselineKeys = db.ShortSnapshots
            .Where(s => s.SnapshotDate <= windowStart)
            .GroupBy(s => s.Lei)
            .Select(g => new { Lei = g.Key, Date = g.Max(s => s.SnapshotDate) });
        var baselineRows = await (
                from s in db.ShortSnapshots.AsNoTracking()
                join k in baselineKeys on new { s.Lei, s.SnapshotDate } equals new { k.Lei, SnapshotDate = k.Date }
                select new Snap(s.Lei, s.SnapshotDate, s.PositionPercent, s.LatestPositionDate))
            .ToListAsync(ct);

        var snaps = baselineRows.Concat(windowRows);

        var candidates = snaps.GroupBy(s => s.Lei).Select(g =>
        {
            var asc = g.OrderBy(s => s.Date).ToList();
            var series = asc.Select(s => new SeriesPointDto(s.Date, s.Pct)).ToList();
            var (change, since) = ShortTrend.ComputeChange(series, today, days);
            return (Current: asc[^1], Change: change, Since: since);
        }).ToList();

        var ordered = sort switch
        {
            ShortSort.Highest => candidates.Where(c => c.Current.Pct > 0).OrderByDescending(c => c.Current.Pct),
            ShortSort.Increase => candidates.Where(c => c.Change > 0).OrderByDescending(c => c.Change),
            _ => candidates.Where(c => c.Change < 0).OrderBy(c => c.Change),
        };
        var top = ordered.ThenBy(c => c.Current.Lei, StringComparer.Ordinal).Take(Math.Max(limit, 0)).ToList();
        if (top.Count == 0) return new ShortTopListDto(updatedAt, []);

        var leis = top.Select(t => t.Current.Lei).ToList();

        var issuers = await db.ShortIssuers.AsNoTracking()
            .Where(i => leis.Contains(i.Lei))
            .ToDictionaryAsync(i => i.Lei, ct);

        var symbolRows = await db.ShortIssuerSymbols.AsNoTracking()
            .Where(s => leis.Contains(s.Lei))
            .ToListAsync(ct);
        var symbolsByLei = symbolRows.GroupBy(s => s.Lei)
            .ToDictionary(g => g.Key, g => g.Select(s => s.Symbol).ToHashSet());

        var holderEvents = await db.ShortPositions.AsNoTracking()
            .Where(p => p.Lei != null && leis.Contains(p.Lei))
            .Select(p => new HolderEvent(p.Lei!, p.HolderName, p.Isin, p.PositionPercent, p.BelowThreshold, p.PositionDate, p.Id, p.InCurrentFile))
            .ToListAsync(ct);
        var fileKnown = await FileKnownAsync(ct);
        var holderCounts = holderEvents.GroupBy(e => e.Lei)
            .ToDictionary(g => g.Key, g => LatestActiveHolders(g, fileKnown).Count);

        var allSymbols = symbolRows.Select(s => s.Symbol).Distinct().ToList();
        var since90 = InsiderCutoff(time);
        var trades = await db.InsiderTrades.AsNoTracking()
            .Where(t => t.PublishingDate >= since90 && t.Symbol != null && allSymbols.Contains(t.Symbol))
            .Select(t => new { t.Symbol, t.TransactionType })
            .ToListAsync(ct);

        var items = top.Select(t =>
        {
            var lei = t.Current.Lei;
            issuers.TryGetValue(lei, out var issuer);
            var syms = symbolsByLei.GetValueOrDefault(lei) ?? [];
            var mine = trades.Where(x => syms.Contains(x.Symbol!)).ToList();
            return new ShortListItemDto(
                issuer?.Name ?? lei, lei, issuer?.PrimarySymbol, t.Current.Pct, t.Current.LatestPos,
                t.Change, t.Since, holderCounts.GetValueOrDefault(lei),
                mine.Count(x => x.TransactionType.ToLower() == "förvärv"),
                mine.Count(x => x.TransactionType.ToLower() == "avyttring"));
        }).ToList();

        return new ShortTopListDto(updatedAt, items);
    }

    public async Task<ShortCompanyDto?> GetCompanyAsync(string symbol, CancellationToken ct)
    {
        var key = symbol.Trim().ToUpperInvariant();
        if (key.EndsWith(".ST")) key = key[..^3];
        if (key.Length == 0) return null;

        // Stored symbols may carry a ".ST" suffix; accept both forms.
        var withSuffix = key + ".ST";
        var lei = await db.ShortIssuerSymbols.AsNoTracking()
            .Where(s => s.Symbol == key || s.Symbol == withSuffix)
            .OrderBy(s => s.Lei)
            .Select(s => s.Lei)
            .FirstOrDefaultAsync(ct);
        if (lei is null) return null;

        var snaps = await db.ShortSnapshots.AsNoTracking()
            .Where(s => s.Lei == lei)
            .OrderBy(s => s.SnapshotDate)
            .Select(s => new Snap(s.Lei, s.SnapshotDate, s.PositionPercent, s.LatestPositionDate))
            .ToListAsync(ct);
        if (snaps.Count == 0) return null;

        var issuer = await db.ShortIssuers.AsNoTracking().FirstOrDefaultAsync(i => i.Lei == lei, ct);
        var events = await db.ShortPositions.AsNoTracking().Where(p => p.Lei == lei).ToListAsync(ct);

        var today = Today;
        var total = snaps.Select(s => new SeriesPointDto(s.Date, s.Pct)).ToList();
        var current = snaps[^1];
        var (change, since) = ShortTrend.ComputeChange(total, today, 30);

        var fileKnown = await FileKnownAsync(ct);
        var holders = LatestActiveHolders(events.Select(p =>
                new HolderEvent(lei, p.HolderName, p.Isin, p.PositionPercent, p.BelowThreshold, p.PositionDate, p.Id, p.InCurrentFile)), fileKnown)
            .Select(h => new ShortHolderDto(h.HolderName, h.Pct, h.Date))
            .ToList();

        return new ShortCompanyDto(
            issuer?.Name ?? events.FirstOrDefault()?.IssuerName ?? lei, lei, issuer?.PrimarySymbol,
            current.Pct, current.LatestPos, change, since, holders, total,
            ShortTrend.SignificantSeries(events, today, fileKnown));
    }

    // Rows are only marked once ingestion has run with the InCurrentFile column; before that
    // (or in a DB populated earlier) fall back to the latest-row-per-holder heuristic.
    private Task<bool> FileKnownAsync(CancellationToken ct) => db.ShortPositions.AnyAsync(p => p.InCurrentFile, ct);

    private static List<HolderEvent> LatestActiveHolders(IEnumerable<HolderEvent> events, bool fileKnown)
    {
        if (fileKnown)
            // FI's current file is the authoritative list of open positions.
            return events.Where(e => e.InFile && !e.Below && e.Pct > 0)
                .GroupBy(e => (e.HolderName, e.Isin))
                .Select(g => g.OrderByDescending(e => e.Date).ThenByDescending(e => e.Id).First())
                .OrderByDescending(e => e.Pct)
                .ThenBy(e => e.HolderName, StringComparer.Ordinal)
                .ToList();

        return events.GroupBy(e => ShortTrend.HolderKey(e.HolderName))
            .Select(g => g.OrderByDescending(e => e.Date).ThenByDescending(e => e.Id).First())
            .Where(e => !e.Below && e.Pct > 0)
            .OrderByDescending(e => e.Pct)
            .ThenBy(e => e.HolderName, StringComparer.Ordinal)
            .ToList();
    }
}
