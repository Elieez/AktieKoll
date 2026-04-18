using AktieKoll.Data;
using AktieKoll.Extensions;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AktieKoll.Services;

public class SymbolService(
    ApplicationDbContext context,
    IMemoryCache cache,
    ILogger<SymbolService> logger) : ISymbolService
{
    private const string CacheKey = "companies:symbol-cache";

    private record CachedEntry(string Code, string? Isin, string NormalizedIsin, string NormalizedName);

    public void InvalidateCache() => cache.Remove(CacheKey);

    public async Task<string?> ResolveSymbolAsync(string? companyName, string? isin)
    {
        var companies = await GetCachedCompaniesAsync();
        return Resolve(companyName, isin, companies);
    }

    public async Task ResolveSymbolsAsync(List<InsiderTrade> trades)
    {
        var pending = trades.Where(t => string.IsNullOrEmpty(t.Symbol)).ToList();
        if (pending.Count == 0) return;

        logger.LogInformation("Resolving symbols for {Count} trades", pending.Count);

        var companies = await GetCachedCompaniesAsync();

        int byIsin = 0, byName = 0, notFound = 0;

        foreach (var trade in pending)
        {
            var symbol = Resolve(trade.CompanyName, trade.Isin, companies);
            if (symbol != null)
            {
                trade.Symbol = symbol;
                // Track rough breakdown for the log summary
                if (!string.IsNullOrWhiteSpace(trade.Isin) &&
                    companies.Any(c => c.Isin == trade.Isin || NormalizeIsin(c.Isin) == NormalizeIsin(trade.Isin)))
                    byIsin++;
                else
                    byName++;
            }
            else
            {
                logger.LogWarning(
                    "No symbol found — CompanyName: '{Company}', ISIN: '{Isin}'",
                    trade.CompanyName, trade.Isin ?? "none");
                notFound++;
            }
        }

        logger.LogInformation(
            "Symbol resolution complete: ~{ByIsin} by ISIN, ~{ByName} by name, {NotFound} not found",
            byIsin, byName, notFound);
    }

    // ── Core resolution logic (single trade) ─────────────────────────────────

    private string? Resolve(string? companyName, string? isin, List<CachedEntry> companies)
    {
        // 1. ISIN exact match
        if (!string.IsNullOrWhiteSpace(isin))
        {
            var exactIsin = companies.Where(c => c.Isin == isin).ToList();
            if (exactIsin.Count > 0)
                return PickBestTicker(exactIsin.Select(c => c.Code));

            // 2. ISIN fuzzy (strip spaces, dashes, uppercase)
            var normIsin = NormalizeIsin(isin);
            if (!string.IsNullOrEmpty(normIsin))
            {
                var fuzzyIsin = companies.Where(c => c.NormalizedIsin == normIsin).ToList();
                if (fuzzyIsin.Count > 0)
                    return PickBestTicker(fuzzyIsin.Select(c => c.Code));
            }
        }

        if (!string.IsNullOrWhiteSpace(companyName))
        {
            var normName = companyName.FilterCompanyName().ToLower();

            // 3. Name normalised exact match
            var exactName = companies.Where(c => c.NormalizedName == normName).ToList();
            if (exactName.Count > 0)
                return PickBestTicker(exactName.Select(c => c.Code));

            // 4. Jaccard token similarity ≥ 0.8
            var best = companies
                .Select(c => (c, sim: JaccardTokenSimilarity(normName, c.NormalizedName)))
                .Where(x => x.sim >= 0.8)
                .OrderByDescending(x => x.sim)
                .ToList();

            if (best.Count > 0)
            {
                var topSim = best[0].sim;
                var topCodes = best.Where(x => x.sim == topSim).Select(x => x.c.Code);
                logger.LogDebug("Fuzzy match: '{Name}' → sim={Sim:F2}", companyName, topSim);
                return PickBestTicker(topCodes);
            }
        }

        return null;
    }

    // ── Cache ─────────────────────────────────────────────────────────────────

    private async Task<List<CachedEntry>> GetCachedCompaniesAsync()
    {
        if (cache.TryGetValue(CacheKey, out List<CachedEntry>? cached) && cached != null)
            return cached;

        logger.LogInformation("Loading companies into symbol cache");

        var companies = await context.Companies
            .Select(c => new { c.Code, c.Isin, c.Name })
            .ToListAsync();

        var entries = companies.Select(c => new CachedEntry(
            c.Code,
            c.Isin,
            NormalizeIsin(c.Isin),
            (c.Name ?? string.Empty).FilterCompanyName().ToLower()
        )).ToList();

        cache.Set(CacheKey, entries);
        return entries;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string NormalizeIsin(string? isin) =>
        isin?.Replace(" ", "").Replace("-", "").ToUpperInvariant() ?? string.Empty;

    private static double JaccardTokenSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

        var tokensA = new HashSet<string>(a.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var tokensB = new HashSet<string>(b.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        var intersection = tokensA.Intersect(tokensB).Count();
        var union = tokensA.Union(tokensB).Count();

        return union == 0 ? 0.0 : (double)intersection / union;
    }

    private static string PickBestTicker(IEnumerable<string> tickers)
    {
        var list = tickers.ToList();
        if (list.Count == 1) return list[0];

        return list.FirstOrDefault(t => t.EndsWith("-B"))
            ?? list.FirstOrDefault(t => t.EndsWith("-A"))
            ?? list.FirstOrDefault(t => !t.Contains('-'))
            ?? list[0];
    }
}