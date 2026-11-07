using AktieKoll.Models;

namespace AktieKoll.Interfaces;

public interface IInsiderTradeService
{
    Task<InsiderTradeIngestionResult> AddInsiderTrades(List<InsiderTrade> insiderTrades);
    Task<IEnumerable<InsiderTrade>> GetInsiderTradesPage(int page, int pageSize);
    Task<IEnumerable<InsiderTrade>> GetInsiderTradesTop();
    Task<IEnumerable<CompanyTransactionStats>> GetTransactionCountBuy(string? symbol = null, int days = 30, int? top = 5);
    Task<IEnumerable<CompanyTransactionStats>> GetTransactionCountSell(string? symbol = null, int days = 30, int? top = 5);
    Task<IEnumerable<InsiderTrade>> GetInsiderTradesByCompany(string companyName, int skip = 0, int take = 10);
    Task<IEnumerable<InsiderTrade>> GetInsiderTradesBySymbol(string symbol, int skip = 0, int take = 10);
    Task<YtdStats> GetYtdTransactionStatsAsync();
    Task<IEnumerable<TradedSymbol>> GetTradedSymbolsAsync();
    /// <summary>Fills new detail columns (LEI, venue, flags, ValueSek) on already stored trades. Returns rows updated.</summary>
    Task<BackfillResult> BackfillTradeDetailsAsync(List<InsiderTrade> freshTrades);
}