using AktieKoll.Models;

namespace AktieKoll.Interfaces;

public interface ISymbolService
{
    Task ResolveSymbols(List<InsiderTrade> newTrades, List<InsiderTrade> existingTrades, CancellationToken ct = default);
}