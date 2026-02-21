using AktieKoll.Models;

namespace AktieKoll.Interfaces;

public interface ISymbolService
{
    Task ResolveSymbolsAsync(List<InsiderTrade> trades);
}