using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Services;

public class SymbolService(ApplicationDbContext context, ILogger<SymbolService> logger) : ISymbolService
{
    public async Task ResolveSymbolsAsync(List<InsiderTrade> trades)
    {
        var uniqueIsins = trades
            .Where(t => !string.IsNullOrEmpty(t.Isin) && string.IsNullOrEmpty(t.Symbol))
            .Select(t => t.Isin!)
            .Distinct()
            .ToList();

        if (uniqueIsins.Count == 0)
        {
            logger.LogInformation("No ISINs need symbol resolution");
            return;
        }

        logger.LogInformation("Resolving symbols for {Count} unique ISINs", uniqueIsins.Count);

        var companiesByIsin = await context.Companies
            .Where(c => c.Isin != null && uniqueIsins.Contains(c.Isin))
            .ToDictionaryAsync(c => c.Isin!, c => c.Code);

        var unresolvedCompanyNames = trades
            .Where(t => !string.IsNullOrEmpty(t.Isin) &&
                        string.IsNullOrEmpty(t.Symbol) &&
                        !companiesByIsin.ContainsKey(t.Isin))
            .Select(t => NormalizeCompanyName(t.CompanyName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Dictionary<string, string> companiesByName = [];

        if (unresolvedCompanyNames.Count > 0)
        {
            logger.LogInformation("Attempting to resolve symbols for {Count} companies by name", 
                unresolvedCompanyNames.Count);

            var allCompanies = await context.Companies
                .Select(c => new { c.Name, c.Code })
                .ToListAsync();

            companiesByName = allCompanies
                .GroupBy(c => NormalizeCompanyName(c.Name))
                .Where(g => unresolvedCompanyNames.Contains(g.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(
                    g => g.Key,
                    g => PickBestTicker(g.Select(x => x.Code).ToList()),
                    StringComparer.OrdinalIgnoreCase);
        }

        int resolvedByIsin = 0;
        int resolvedByName = 0;
        int notFound = 0;

        foreach (var trade in trades)
        {
            if (string.IsNullOrEmpty(trade.Isin) || !string.IsNullOrEmpty(trade.Symbol))
                continue;

            if (companiesByIsin.TryGetValue(trade.Isin, out var codeByIsin))
            {
                trade.Symbol = codeByIsin;
                resolvedByIsin++;
            }
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
                    logger.LogWarning("No symbol found for ISIN: {Isin} (Company: {Company})",
                        trade.Isin, trade.CompanyName);
                    notFound++;
                }
            }
        }

        logger.LogInformation("Symbol resolution complete: {ResolvedByIsin} by ISIN, {ResolvedByName} by name fallback, {NotFound} not found", 
            resolvedByIsin, resolvedByName, notFound);
    }

    private static string NormalizeCompanyName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        return name
            .Trim()
            .Replace(" AB", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" (publ)", "", StringComparison.OrdinalIgnoreCase)
            .Replace("AB ", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
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
        var noSuffix = tickers.FirstOrDefault(t => !t.Contains("-"));
        if (noSuffix != null)
            return noSuffix;

        // 4. Fallback to first
        return tickers[0];
    }
}