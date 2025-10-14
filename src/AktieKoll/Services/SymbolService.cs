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
        var symbolCache = existingTrades
            .Where(t => !string.IsNullOrWhiteSpace(t.CompanyName) && !string.IsNullOrWhiteSpace(t.Symbol))
            .ToDictionary(t => t.CompanyName!, t => t.Symbol!, StringComparer.OrdinalIgnoreCase);

        // ISIN -> Symbol (from existing)
        var byIsin = existingTrades
            .Where(t => !string.IsNullOrWhiteSpace(t.Isin) && !string.IsNullOrWhiteSpace(t.Symbol))
            .ToDictionary(t => t.Isin!, t => t.Symbol!, StringComparer.OrdinalIgnoreCase);

        // Per-run dedupe of FIGI lookups: ISIN -> Symbol
        var runIsin = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var trade in newTrades)
        {
            ct.ThrowIfCancellationRequested();

            // If already has a symbol, seed caches and continue
            if (!string.IsNullOrWhiteSpace(trade.Symbol))
            {
                if (!string.IsNullOrWhiteSpace(trade.Isin))
                    byIsin.TryAdd(trade.Isin!, trade.Symbol!); // <-- fixed parentheses & semicolon

                if (!string.IsNullOrWhiteSpace(trade.CompanyName))
                    symbolCache.TryAdd(trade.CompanyName!, trade.Symbol!);

                continue;
            }

            string? resolved = null;

            // Try ISIN-based caches first
            if (!string.IsNullOrWhiteSpace(trade.Isin))
            {
                if (byIsin.TryGetValue(trade.Isin!, out var s) || runIsin.TryGetValue(trade.Isin!, out s))
                {
                    resolved = s;
                }
                else
                {
                    // FIGI lookup once per ISIN in this run
                    resolved = await figiService.GetTickerByIsinAsync(trade.Isin!, ct); // <-- ct now in scope
                    if (!string.IsNullOrWhiteSpace(resolved))
                    {
                        runIsin[trade.Isin!] = resolved!;
                        byIsin[trade.Isin!] = resolved!;
                    }
                }
            }

            // Fallback: name cache
            if (resolved is null && !string.IsNullOrWhiteSpace(trade.CompanyName))
                symbolCache.TryGetValue(trade.CompanyName!, out resolved);

            if (!string.IsNullOrWhiteSpace(resolved))
            {
                trade.Symbol = resolved!;
                if (!string.IsNullOrWhiteSpace(trade.CompanyName))
                    symbolCache.TryAdd(trade.CompanyName!, resolved!);
            }
        }
    }
}
