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
            .Select(t => t.CompanyName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Dictionary<string, string> companiesByName = new();

        if (unresolvedCompanyNames.Count > 0)
        {
            logger.LogInformation("Attempting to resolve symbols for {Count} companies by name", 
                unresolvedCompanyNames.Count);

            companiesByName = await context.Companies
                .Where(c => unresolvedCompanyNames.Contains(c.Name))
                .ToDictionaryAsync(c => c.Name, c => c.Code, StringComparer.OrdinalIgnoreCase);
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

            else if (companiesByName.TryGetValue(trade.CompanyName.Trim(), out var codeByName))
            {
                trade.Symbol = codeByName;
                resolvedByName++;
                logger.LogInformation("Resolved by name fallback: {Company} (ISIN: {Isin}) → {Symbol}",
                    trade.CompanyName, trade.Isin, codeByName);
            }
            else
            {
                logger.LogWarning("No symbol found for ISIN: {Isin} (Company: {Company})", 
                    trade.Isin, trade.CompanyName);
                notFound++;
            }
        }

        logger.LogInformation("Symbol resolution complete: {ResolvedByIsin} by ISIN, {ResolvedByName} by name fallback, {NotFound} not found", 
            resolvedByIsin, resolvedByName, notFound);
    }
}