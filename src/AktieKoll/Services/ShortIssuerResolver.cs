using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Services;

public record IssuerMap(
    IReadOnlyDictionary<string, string> LeiByIsin,
    IReadOnlyDictionary<string, string> LeiByName);

/// <summary>
/// Maps FI's short-register issuers (keyed by LEI) to ISINs and stock tickers, persisting
/// <see cref="ShortIssuer"/> and <see cref="ShortIssuerSymbol"/> rows.
/// </summary>
public class ShortIssuerResolver(
    ApplicationDbContext db,
    IGleifClient gleif,
    ISymbolService symbols,
    TimeProvider time,
    ILogger<ShortIssuerResolver> logger)
{
    private static readonly TimeSpan GleifThrottle = TimeSpan.FromDays(7);

    public async Task<IssuerMap> ResolveAsync(
        IReadOnlyList<FiAggregateRow> aggregate,
        IReadOnlyList<FiPositionRow> positions,
        CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var yearAgo = now.AddDays(-365);

        // 1. Upsert issuers (one per LEI; first row wins on duplicates).
        var rows = aggregate
            .Where(a => !string.IsNullOrWhiteSpace(a.Lei))
            .GroupBy(a => a.Lei)
            .Select(g => g.First())
            .ToList();
        var leis = rows.Select(r => r.Lei).ToList();

        var issuers = (await db.ShortIssuers.Where(i => leis.Contains(i.Lei)).ToListAsync(ct))
            .ToDictionary(i => i.Lei);
        foreach (var row in rows)
        {
            var name = FiShortFileParser.NormalizeName(row.IssuerName);
            if (issuers.TryGetValue(row.Lei, out var existing))
            {
                if (existing.Name != name) existing.Name = name;
            }
            else
            {
                var created = new ShortIssuer { Lei = row.Lei, Name = name };
                db.ShortIssuers.Add(created);
                issuers[row.Lei] = created;
            }
        }

        // 2a. ISINs from position rows with the same issuer name.
        var isinsByName = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in positions)
        {
            if (string.IsNullOrWhiteSpace(p.Isin)) continue;
            var key = FiShortFileParser.NormalizeName(p.IssuerName);
            if (!isinsByName.TryGetValue(key, out var set))
                isinsByName[key] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            set.Add(p.Isin.Trim());
        }

        // 2b. Insider trades that carry the LEI.
        var trades = await db.InsiderTrades
            .Where(t => t.Lei != null && leis.Contains(t.Lei))
            .Select(t => new { Lei = t.Lei!, t.Isin, t.Symbol, t.PublishingDate })
            .ToListAsync(ct);
        var tradesByLei = trades.ToLookup(t => t.Lei);

        var isinsByLei = new Dictionary<string, HashSet<string>>();
        foreach (var row in rows)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (isinsByName.TryGetValue(FiShortFileParser.NormalizeName(row.IssuerName), out var fromFile))
                set.UnionWith(fromFile);
            foreach (var t in tradesByLei[row.Lei])
                if (!string.IsNullOrWhiteSpace(t.Isin)) set.Add(t.Isin);

            // 2c. GLEIF, only when nothing else produced an ISIN, at most once per 7 days.
            var issuer = issuers[row.Lei];
            if (set.Count == 0 && (issuer.GleifCheckedAt is null || now - issuer.GleifCheckedAt.Value > GleifThrottle))
            {
                var fromGleif = await gleif.GetIsinsAsync(row.Lei, ct);
                if (fromGleif.Count > 0)
                {
                    var wanted = fromGleif.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    var known = await db.Companies
                        .Where(c => c.Isin != null && wanted.Contains(c.Isin))
                        .Select(c => c.Isin!)
                        .ToListAsync(ct);
                    set.UnionWith(known);
                }
                issuer.GleifCheckedAt = now;
            }

            isinsByLei[row.Lei] = set;
        }

        // 3. Tickers: companies by ISIN, plus symbols seen on the LEI's insider trades.
        var allIsins = isinsByLei.Values.SelectMany(s => s).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var companyCodes = (await db.Companies
                .Where(c => c.Isin != null && allIsins.Contains(c.Isin))
                .Select(c => new { Isin = c.Isin!, c.Code })
                .ToListAsync(ct))
            .ToLookup(c => c.Isin, StringComparer.OrdinalIgnoreCase);

        // Insider trades also cover an issuer's bonds, whose "symbol" is a bond name
        // (e.g. "AFRYSS F 12/01/26 dmtn"), so only accept symbols that are listed tickers.
        var tradeSymbols = trades
            .Where(t => !string.IsNullOrWhiteSpace(t.Symbol))
            .Select(t => t.Symbol!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var listedTradeSymbols = (await db.Companies
                .Where(c => tradeSymbols.Contains(c.Code))
                .Select(c => c.Code)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var existingSymbols = (await db.ShortIssuerSymbols.Where(s => leis.Contains(s.Lei)).ToListAsync(ct))
            .ToLookup(s => s.Lei);

        var leiByIsin = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var leiByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var unresolved = new List<string>();
        var resolved = 0;

        foreach (var row in rows)
        {
            var lei = row.Lei;
            var isins = isinsByLei[lei];
            var tickers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var isin in isins)
                foreach (var c in companyCodes[isin])
                    tickers.Add(c.Code);
            foreach (var t in tradesByLei[lei])
                if (t.Symbol is not null && listedTradeSymbols.Contains(t.Symbol)) tickers.Add(t.Symbol);

            if (tickers.Count == 0)
            {
                var fallback = await symbols.ResolveSymbolAsync(row.IssuerName, isins.OrderBy(i => i, StringComparer.Ordinal).FirstOrDefault());
                if (!string.IsNullOrWhiteSpace(fallback)) tickers.Add(fallback);
            }

            // Replace the issuer's symbol rows (diffed, so unchanged keys are untouched).
            var current = existingSymbols[lei].ToList();
            db.ShortIssuerSymbols.RemoveRange(current.Where(s => !tickers.Contains(s.Symbol)));
            var have = current.Select(s => s.Symbol).ToHashSet(StringComparer.Ordinal);
            foreach (var ticker in tickers.Where(t => !have.Contains(t)))
                db.ShortIssuerSymbols.Add(new ShortIssuerSymbol { Lei = lei, Symbol = ticker });

            // 4. Primary = most insider trades in the last 365 days, ties prefer the B class (as SymbolService), then alphabetical.
            var recent = tradesByLei[lei]
                .Where(t => t.PublishingDate >= yearAgo && t.Symbol != null)
                .GroupBy(t => t.Symbol!)
                .ToDictionary(g => g.Key, g => g.Count());
            issuers[lei].PrimarySymbol = tickers
                .OrderByDescending(t => recent.GetValueOrDefault(t))
                .ThenByDescending(t => t.EndsWith("-B", StringComparison.Ordinal))
                .ThenBy(t => t, StringComparer.Ordinal)
                .FirstOrDefault();

            if (tickers.Count > 0) resolved++;
            else unresolved.Add(issuers[lei].Name);

            foreach (var isin in isins) leiByIsin.TryAdd(isin, lei);
            leiByName.TryAdd(FiShortFileParser.NormalizeName(row.IssuerName), lei);
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Short issuers: {Issuers} total, {Resolved} resolved, {Unresolved} unresolved. First unresolved: {Names}",
            rows.Count, resolved, unresolved.Count, string.Join("; ", unresolved.Take(20)));

        return new IssuerMap(leiByIsin, leiByName);
    }
}
