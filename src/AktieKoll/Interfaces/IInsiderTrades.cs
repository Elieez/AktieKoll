using AktieKoll.Models;

namespace AktieKoll.Interfaces;

public interface IInsiderTradeService
{
    Task<string> AddInsiderTrades(List<InsiderTrade> insiderTrades);
    Task<IEnumerable<InsiderTrade>> GetInsiderTrades();
    Task<IEnumerable<InsiderTrade>> GetInsiderTradesTop();
    Task<IEnumerable<CompanyTransactionStats>> GetCompaniesCountBuy(int days = 30, int top = 5);
    Task<IEnumerable<CompanyTransactionStats>> GetCompaniesCountSell(int days = 30, int top = 5);
    Task<IEnumerable<InsiderTrade>> GetInsiderTradesByCompany(string companyName);
}
