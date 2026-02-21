using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Services;

public class SymbolService(ApplicationDbContext context, ILogger<SymbolService> logger) : ISymbolService
{
    public async Task ResolveSymbolsAsync(List<InsiderTrade> trades)
    {
        var isins = trades
            .Where(t => !string.IsNullOrEmpty(t.Isin) && string.IsNullOrEmpty(t.Symbol))
            .Select(t => t.Isin!)
            .Distinct()
            .ToList();

        if (isins.Count == 0)
        {
            logger.LogInformation("No ISINs need symbol resolution");
            return;
        }

        logger.LogInformation("Resolving symbols for {Count} ISINs", isins.Count);

        var companies = await context.Companies
            .Where(c => c.Isin != null && isins.Contains(c.Isin))
            .ToDictionaryAsync(c => c.Isin!, c => c.Code);

        int resolved = 0;
        int notFound = 0;

        foreach (var trade in trades)
        {
            if (string.IsNullOrEmpty(trade.Isin))
                continue;

            if (companies.TryGetValue(trade.Isin, out var code))
            {
                trade.Symbol = code;
                resolved++;
            }
            else
            {
                logger.LogWarning("No symbol found for ISIN: {Isin} (Company: {Company})", 
                    trade.Isin, trade.CompanyName);
                notFound++;
            }
        }

        logger.LogInformation("Resolved {Resolved} symbols, {NotFound} not found", resolved, notFound);
    }
}