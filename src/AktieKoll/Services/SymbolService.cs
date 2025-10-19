using AktieKoll.Interfaces;
using AktieKoll.Models;

namespace AktieKoll.Services;

public class SymbolService(IOpenFigiService figiService) : ISymbolService
{
    public async Task ResolveSymbols(
        List<InsiderTrade> newTrades,
        List<InsiderTrade> existingTrades,
        CancellationToken ct = default)
    {
        // CompanyName -> Symbol (from existing)
        var symbolCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach(var t in existingTrades)
        {
            var name = t.CompanyName;
            var symbol = t.Symbol;
            if(!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(symbol))
            {
                symbolCache.TryAdd(name, symbol);
            }
        }

        // ISIN -> Symbol (from existing)
        var byIsin = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach(var t in existingTrades)
        {
            var isin = t.Isin;
            var symbol = t.Symbol;
            if(!string.IsNullOrWhiteSpace(isin) && !string.IsNullOrWhiteSpace(symbol))
            {
                byIsin.TryAdd(isin, symbol);
            }
        }

        // Per-run dedupe of FIGI lookups: ISIN -> Symbol
        var runIsin = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var trade in newTrades)
        {
            ct.ThrowIfCancellationRequested();

            // If already has a symbol, seed caches and continue
            if (!string.IsNullOrWhiteSpace(trade.Symbol))
            {
                if (!string.IsNullOrWhiteSpace(trade.Isin))
                    byIsin.TryAdd(trade.Isin, trade.Symbol); // <-- fixed parentheses & semicolon

                if (!string.IsNullOrWhiteSpace(trade.CompanyName))
                    symbolCache.TryAdd(trade.CompanyName, trade.Symbol);

                continue;
            }

            string? resolved = null;

            // Try ISIN-based caches first
            var tradeIsin = trade.Isin;
            if (!string.IsNullOrWhiteSpace(tradeIsin))
            {
                if (byIsin.TryGetValue(tradeIsin, out var s) || runIsin.TryGetValue(tradeIsin, out s))
                {
                    resolved = s;
                }
                else
                {
                    // FIGI lookup once per ISIN in this run
                    resolved = await figiService.GetTickerByIsinAsync(tradeIsin, ct); // <-- ct now in scope
                    if (!string.IsNullOrWhiteSpace(resolved))
                    {
                        runIsin[tradeIsin] = resolved;
                        byIsin[tradeIsin] = resolved;
                    }
                }
            }

            // Fallback: name cache
            var tradeName = trade.CompanyName;
            if (resolved is null && !string.IsNullOrWhiteSpace(tradeName))
                symbolCache.TryGetValue(tradeName, out resolved);

            if (!string.IsNullOrWhiteSpace(resolved))
            {
                trade.Symbol = resolved!;
                if (!string.IsNullOrWhiteSpace(tradeName))
                    symbolCache.TryAdd(tradeName, resolved);
            }
        }
    }
}