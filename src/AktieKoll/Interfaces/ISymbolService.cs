using AktieKoll.Models;

namespace AktieKoll.Interfaces;

public interface ISymbolService
{
    Task ResolveSymbolsAsync(List<InsiderTrade> trades);
    Task<string?> ResolveSymbolAsync(string? companyName, string? isin);
    void InvalidateCache();
}