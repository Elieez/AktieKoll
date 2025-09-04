using AktieKoll.Interfaces;
using AktieKoll.Models;

namespace AktieKoll.Services;

public class SymbolService(IOpenFigiService figiService) : ISymbolService
{
    public async Task ResolveSymbols(List<InsiderTrade> newTrades, List<InsiderTrade> existingTrades)
    {
        var symbolCache = existingTrades
            .Where(t => !string.IsNullOrWhiteSpace(t.Symbol))
            .ToDictionary(t => t.CompanyName, t => t.Symbol!, StringComparer.OrdinalIgnoreCase);

        foreach (var trade in newTrades)
        {
            if (string.IsNullOrWhiteSpace(trade.Symbol))
            {
                if (symbolCache.TryGetValue(trade.CompanyName, out var cached))
                {
                    trade.Symbol = cached;
                }
                else if (!string.IsNullOrWhiteSpace(trade.Isin))
                {
                    var resolved = await figiService.GetTickerByIsinAsync(trade.Isin);
                    if (!string.IsNullOrWhiteSpace(resolved))
                    {
                        trade.Symbol = resolved;
                        symbolCache[trade.CompanyName] = resolved;
                    }
                }
            }
            else if (!symbolCache.ContainsKey(trade.CompanyName))
            {
                symbolCache[trade.CompanyName] = trade.Symbol;
            }
        }
    }
}
