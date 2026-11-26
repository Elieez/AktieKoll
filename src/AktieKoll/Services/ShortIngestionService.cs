using AktieKoll.Data;
using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Services;

/// <summary>
/// Stores FI short positions (current + history) and writes the daily per-issuer snapshot.
/// </summary>
public class ShortIngestionService(
    ApplicationDbContext db,
    ShortIssuerResolver resolver,
    TimeProvider time,
    ILogger<ShortIngestionService> logger)
{
    public async Task<ShortIngestionResult> IngestAsync(
        IReadOnlyList<FiAggregateRow> aggregate,
        IReadOnlyList<FiPositionRow> current,
        IReadOnlyList<FiPositionRow> history,
        CancellationToken ct)
    {
        if (aggregate.Count == 0)
            throw new ArgumentException(
                "Aggregate is empty; refusing to ingest (would falsely mark every issuer as dropped out).",
                nameof(aggregate));

        // Union current + history, first occurrence wins.
        var positions = new List<FiPositionRow>();
        var seen = new HashSet<(string, string, DateTime)>();
        foreach (var p in current.Concat(history))
            if (seen.Add((p.HolderName, p.Isin, p.PositionDate.Date)))
                positions.Add(p);

        var map = await resolver.ResolveAsync(aggregate, positions, ct);

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        var today = TimeZoneInfo.ConvertTime(time.GetUtcNow(), tz).Date;

        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        // Positions: load existing keys once.
        var existing = (await db.ShortPositions
                .Select(p => new { p.HolderName, p.Isin, p.PositionDate })
                .ToListAsync(ct))
            .Select(p => (p.HolderName, p.Isin, p.PositionDate.Date))
            .ToHashSet();

        var newPositions = 0;
        foreach (var p in positions)
        {
            if (existing.Contains((p.HolderName, p.Isin, p.PositionDate.Date))) continue;
            db.ShortPositions.Add(new ShortPosition
            {
                Lei = LookupLei(map, p.Isin, p.IssuerName),
                HolderName = p.HolderName,
                IssuerName = p.IssuerName,
                Isin = p.Isin,
                PositionPercent = p.PositionPercent,
                BelowThreshold = p.BelowThreshold,
                PositionDate = p.PositionDate.Date,
                Comment = p.Comment
            });
            newPositions++;
        }
        await db.SaveChangesAsync(ct);

        // Backfill Lei on stored rows that were unresolved before.
        var backfilled = 0;
        var orphans = await db.ShortPositions.Where(p => p.Lei == null).ToListAsync(ct);
        foreach (var o in orphans)
        {
            var lei = LookupLei(map, o.Isin, o.IssuerName);
            if (lei is null) continue;
            o.Lei = lei;
            backfilled++;
        }

        // Snapshot for today (delete + reinsert so reruns are idempotent).
        var todays = await db.ShortSnapshots.Where(s => s.SnapshotDate == today).ToListAsync(ct);
        db.ShortSnapshots.RemoveRange(todays);
        await db.SaveChangesAsync(ct); // keep the delete and re-insert of the same key in separate batches

        var rows = aggregate
            .Where(a => !string.IsNullOrWhiteSpace(a.Lei))
            .GroupBy(a => a.Lei)
            .Select(g => g.First())
            .ToList();
        foreach (var r in rows)
        {
            db.ShortSnapshots.Add(new ShortSnapshot
            {
                Lei = r.Lei,
                SnapshotDate = today,
                PositionPercent = r.PositionPercent,
                LatestPositionDate = r.LatestPositionDate.Date
            });
        }

        // Drop-outs: latest snapshot before today is > 0 and the issuer is absent today.
        var inToday = rows.Select(r => r.Lei).ToHashSet();
        var prior = await db.ShortSnapshots
            .Where(s => s.SnapshotDate < today)
            .Select(s => new { s.Lei, s.SnapshotDate, s.PositionPercent })
            .ToListAsync(ct);
        var dropped = prior
            .GroupBy(s => s.Lei)
            .Select(g => g.OrderByDescending(s => s.SnapshotDate).First())
            .Where(s => s.PositionPercent > 0 && !inToday.Contains(s.Lei))
            .ToList();
        foreach (var d in dropped)
        {
            db.ShortSnapshots.Add(new ShortSnapshot
            {
                Lei = d.Lei,
                SnapshotDate = today,
                PositionPercent = 0m,
                LatestPositionDate = null
            });
        }

        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);

        var unresolved = await db.ShortIssuers
            .CountAsync(i => inToday.Contains(i.Lei) && i.PrimarySymbol == null, ct);

        logger.LogInformation(
            "Short ingestion {Date:yyyy-MM-dd}: {New} new positions ({Backfilled} Lei backfilled), {Snapshots} snapshots, {Dropped} dropped out, {Unresolved} unresolved issuers",
            today, newPositions, backfilled, rows.Count, dropped.Count, unresolved);

        return new ShortIngestionResult(newPositions, rows.Count, dropped.Count, unresolved);
    }

    private static string? LookupLei(IssuerMap map, string isin, string issuerName)
        => map.LeiByIsin.GetValueOrDefault(isin)
           ?? map.LeiByName.GetValueOrDefault(FiShortFileParser.NormalizeName(issuerName));
}
