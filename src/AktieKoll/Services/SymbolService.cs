using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Services;

public class SymbolService(ApplicationDbContext context, ILogger<SymbolService> logger) : ISymbolService
{
    public async Task ResolveSymbolsAsync(List<InsiderTrade> trades)
    {
        var tradesWithIsin = trades
        .Where(t => !string.IsNullOrEmpty(t.Isin) && string.IsNullOrEmpty(t.Symbol))
        .ToList();

        var tradesWithoutIsin = trades
            .Where(t => string.IsNullOrEmpty(t.Isin) && string.IsNullOrEmpty(t.Symbol))
            .ToList();

        logger.LogInformation("Resolving symbols for {WithIsin} trades with ISIN, {WithoutIsin} without ISIN",
            tradesWithIsin.Count, tradesWithoutIsin.Count);

        // Step 1: Resolve by ISIN (for trades that have ISINs)
        Dictionary<string, string> companiesByIsin = [];

        if (tradesWithIsin.Count > 0)
        {
            var uniqueIsins = tradesWithIsin
                .Select(t => t.Isin!)
                .Distinct()
                .ToList();

            companiesByIsin = await context.Companies
                .Where(c => c.Isin != null && uniqueIsins.Contains(c.Isin))
                .ToDictionaryAsync(c => c.Isin!, c => c.Code);
        }

        // Step 2: Build name fallback for unresolved trades (with OR without ISIN)
        var unresolvedTrades = trades
            .Where(t => string.IsNullOrEmpty(t.Symbol))
            .Where(t => string.IsNullOrEmpty(t.Isin) || !companiesByIsin.ContainsKey(t.Isin))
            .ToList();

        Dictionary<string, string> companiesByName = [];

        if (unresolvedTrades.Count > 0)
        {
            var normalizedNames = unresolvedTrades
                .Select(t => NormalizeCompanyName(t.CompanyName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            logger.LogInformation("Attempting name fallback for {Count} companies", normalizedNames.Count);

            var allCompanies = await context.Companies
                .Select(c => new { c.Name, c.Code })
                .ToListAsync();

            companiesByName = allCompanies
                .GroupBy(c => NormalizeCompanyName(c.Name))
                .Where(g => normalizedNames.Contains(g.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(
                    g => g.Key,
                    g => PickBestTicker(g.Select(x => x.Code).ToList()),
                    StringComparer.OrdinalIgnoreCase);
        }

        // Step 3: Assign symbols
        int resolvedByIsin = 0;
        int resolvedByName = 0;
        int notFound = 0;

        foreach (var trade in trades)
        {
            if (!string.IsNullOrEmpty(trade.Symbol))
                continue;

            // Try ISIN first (if trade has ISIN)
            if (!string.IsNullOrEmpty(trade.Isin) &&
                companiesByIsin.TryGetValue(trade.Isin, out var codeByIsin))
            {
                trade.Symbol = codeByIsin;
                resolvedByIsin++;
            }
            // Fallback to name
            else
            {
                var normalizedName = NormalizeCompanyName(trade.CompanyName);

                if (companiesByName.TryGetValue(normalizedName, out var codeByName))
                {
                    trade.Symbol = codeByName;
                    resolvedByName++;
                    logger.LogInformation("Resolved by name: '{Original}' → '{Normalized}' → {Symbol}",
                        trade.CompanyName, normalizedName, codeByName);
                }
                else
                {
                    logger.LogWarning("No symbol found - ISIN: '{Isin}', Company: '{Company}' (normalized: '{Normalized}')",
                        trade.Isin ?? "none", trade.CompanyName, normalizedName);
                    notFound++;
                }
            }
        }

        logger.LogInformation(
            "Symbol resolution complete: {ResolvedByIsin} by ISIN, {ResolvedByName} by name, {NotFound} not found",
            resolvedByIsin, resolvedByName, notFound);
    }

    private static string NormalizeCompanyName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

         return name
            .Trim()
            .TrimEnd('.', ',', ';')
            .Replace(" AB", "", StringComparison.OrdinalIgnoreCase)
            .Replace("AB ", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" (publ)", "", StringComparison.OrdinalIgnoreCase)
            .Replace("(publ)", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" Series A", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" Series B", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" A", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" B", "", StringComparison.OrdinalIgnoreCase)
            .Trim()
            .Replace("  ", " ");
    }

    private static string PickBestTicker(List<string> tickers)
    {
        if (tickers.Count == 1)
            return tickers[0];

        // Priority order:
        // 1. B-shares (most liquid in Sweden)
        var bShare = tickers.FirstOrDefault(t => t.EndsWith("-B"));
        if (bShare != null)
            return bShare;

        // 2. A-shares
        var aShare = tickers.FirstOrDefault(t => t.EndsWith("-A"));
        if (aShare != null)
            return aShare;

        // 3. No suffix (single share class)
        var noSuffix = tickers.FirstOrDefault(t => !t.Contains('-'));
        if (noSuffix != null)
            return noSuffix;

        // 4. Fallback to first
        return tickers[0];
    }
}