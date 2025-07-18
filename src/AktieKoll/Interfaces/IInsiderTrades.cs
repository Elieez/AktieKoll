using AktieKoll.Models;

namespace AktieKoll.Interfaces;

public interface IInsiderTradeService
{
    Task<string> AddInsiderTrades(List<InsiderTrade> insiderTrades);
    Task<IEnumerable<InsiderTrade>> GetInsiderTrades();
    Task<IEnumerable<InsiderTrade>> GetInsiderTradesTop();
    Task<IEnumerable<CompanyTransactionStats>> GetTransactionCountBuy(string? companyName = null, int days = 30, int? top = 5);
    Task<IEnumerable<CompanyTransactionStats>> GetTransactionCountSell(string? companyName = null, int days = 30, int? top = 5);
    Task<IEnumerable<InsiderTrade>> GetInsiderTradesByCompany(string companyName);
}
