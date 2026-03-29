using AktieKoll.Data;
using AktieKoll.Extensions;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AktieKoll.Services;

public class InsiderTradeService(
    ApplicationDbContext context,
    ISymbolService symbolService, 
    IMemoryCache cache,
    TimeProvider timeProvider,
    ILogger<InsiderTradeService> logger) : IInsiderTradeService
{
    public async Task<InsiderTradeIngestionResult> AddInsiderTrades(List<InsiderTrade> insiderTrades)
    {
        if (insiderTrades == null || insiderTrades.Count == 0)
        {
            return new InsiderTradeIngestionResult("No data provided.", []);
        }

        var dates = insiderTrades.Select(t => t.PublishingDate).Distinct().ToList();

        var existingTrades = await context.InsiderTrades
            .Where(t => dates.Contains(t.PublishingDate))
            .ToListAsync();

        await symbolService.ResolveSymbolsAsync(insiderTrades);

        var inserted = new List<InsiderTrade>();
        int newTradesCount = 0;
        int removedTradesCount = 0;
        foreach (var trade in insiderTrades)
        {
            var duplicate = existingTrades.FindDuplicate(trade);
            if (trade.IsRevised())
            {
                if (duplicate != null)
                {
                    context.InsiderTrades.Remove(duplicate);
                    existingTrades.Remove(duplicate);
                    removedTradesCount++;
                }
                continue;
            }

            if (duplicate != null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(trade.Symbol))
            {
                logger.LogWarning(
                    "Trade unresolved at ingestion - CompanyName: '{Company}', ISIN: '{Isin}'",
                    trade.CompanyName, trade.Isin ?? "none");
            }

            context.InsiderTrades.Add(trade);
            existingTrades.Add(trade);
            inserted.Add(trade);
            newTradesCount++;
        }

        if (newTradesCount > 0 || removedTradesCount > 0)
        {
            await context.SaveChangesAsync();

            if (newTradesCount > 0 && removedTradesCount > 0)
            {
                return new InsiderTradeIngestionResult($"{newTradesCount} new trades added. {removedTradesCount} trades removed.", inserted);
            }
            if (newTradesCount > 0)
            {
                return new InsiderTradeIngestionResult($"{newTradesCount} new trades added.", inserted);
            }
            return new InsiderTradeIngestionResult($"{removedTradesCount} trades removed.", []) ;
        }
        return new InsiderTradeIngestionResult("No new data was added.", []);

    }

    public async Task<IEnumerable<InsiderTrade>> GetInsiderTradesPage(int page, int pageSize)
    {
        var skip = (page - 1) * pageSize;
        return await context.InsiderTrades
            .OrderByDescending(t => t.PublishingDate)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<InsiderTrade>> GetInsiderTradesTop()
    {
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);
        var tomorrow = today.AddDays(1);

        return await context.InsiderTrades
            .Where(t => t.PublishingDate >= yesterday && t.PublishingDate < tomorrow)
            .OrderByDescending(t => t.Price * t.Shares)
            .Take(10)
            .ToListAsync();
    }

    private async Task<IEnumerable<CompanyTransactionStats>> GetTransactionCountByType(string transactionType, string? symbol, int days, int? top)
    {
        var endDate = DateTime.UtcNow.Date.AddDays(1);
        var startDate = endDate.AddDays(-days);

        var upperSymbol = symbol?.ToUpper();
        var searchType = transactionType.ToLower();

        IQueryable<CompanyTransactionStats> grouped = context.InsiderTrades
            .Where(t => t.PublishingDate >= startDate && t.PublishingDate < endDate)
            .Where(t => t.TransactionType.ToLower() == searchType)
            .Where(t => string.IsNullOrWhiteSpace(upperSymbol) || t.Symbol == upperSymbol)
            .GroupBy(t => t.CompanyName)
            .Select(g => new CompanyTransactionStats
            {
                CompanyName = g.Key,
                TransactionCount = g.Count()
            })
            .OrderByDescending(c => c.TransactionCount)
            .ThenBy(c => c.CompanyName);
            
        if (top.HasValue)
        {
            grouped = grouped.Take(top.Value);
        }

        return await grouped.ToListAsync();
    }

    public Task<IEnumerable<CompanyTransactionStats>> GetTransactionCountBuy(string? symbol = null, int days = 30, int? top = 5)
        => GetTransactionCountByType("förvärv", symbol, days, top);
    public Task<IEnumerable<CompanyTransactionStats>> GetTransactionCountSell(string? symbol = null, int days = 30, int? top = 5)
        => GetTransactionCountByType("avyttring", symbol, days, top);

    public async Task<IEnumerable<InsiderTrade>> GetInsiderTradesByCompany(string companyName, int skip = 0, int take = 10)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            return [];
        }

        var filteredCompanyName = companyName.FilterCompanyName().ToLower();

        return await context.InsiderTrades
            .Where(t => t.CompanyName.ToLower() == filteredCompanyName)
            .OrderByDescending(t => t.PublishingDate)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<IEnumerable<InsiderTrade>> GetInsiderTradesBySymbol(string symbol, int skip = 0, int take = 10)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return [];
        }

        return await context.InsiderTrades
            .Where(t => t.Symbol == symbol)
            .OrderByDescending(t => t.PublishingDate)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<YtdStats> GetYtdTransactionStatsAsync()
    {
        const string cacheKey = "trades:ytd-stats";

        if (cache.TryGetValue(cacheKey, out YtdStats? cachedStats) && cachedStats != null)
            return cachedStats;

        var startOfYear = new DateTime(timeProvider.GetUtcNow().Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var stats = await context.InsiderTrades
             .Where(t => t.PublishingDate >= startOfYear)
             .GroupBy(_ => 1)
             .Select(g => new YtdStats
             {
                 TotalTransactions = (long)g.Count(),
                 TotalValue = g.Sum(t => t.Price * t.Shares)
             })
             .FirstOrDefaultAsync();

        cache.Set(cacheKey, stats, new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromHours(6)));

        return stats ?? new YtdStats { TotalTransactions = 0, TotalValue = 0 };
    }
}