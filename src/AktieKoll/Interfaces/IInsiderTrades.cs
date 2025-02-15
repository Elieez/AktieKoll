using AktieKoll.Models;

namespace AktieKoll.Interfaces
{
    public interface IInsiderTradeService
    {
        Task<string> AddInsiderTrades(List<InsiderTrade> insiderTrades);
        Task<IEnumerable<InsiderTrade>> GetInsiderTrades();
    }
}
