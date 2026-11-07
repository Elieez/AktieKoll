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
        var today = timeProvider.GetUtcNow().UtcDateTime.Date;
        var yesterday = today.AddDays(-1);
        var tomorrow = today.AddDays(1);

        return await context.InsiderTrades
            .Where(t => t.PublishingDate >= yesterday && t.PublishingDate < tomorrow)
            .OrderByDescending(t => t.ValueSek ?? t.Price * t.Shares)
            .Take(10)
            .ToListAsync();
    }

    private async Task<IEnumerable<CompanyTransactionStats>> GetTransactionCountByType(string transactionType, string? symbol, int days, int? top)
    {
        var endDate = timeProvider.GetUtcNow().UtcDateTime.Date.AddDays(1);
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

    public async Task<IEnumerable<TradedSymbol>> GetTradedSymbolsAsync()
    {
        const string cacheKey = "trades:traded-symbols";

        if (cache.TryGetValue(cacheKey, out List<TradedSymbol>? cached) && cached != null)
            return cached;

        var symbols = await context.InsiderTrades
            .Where(t => t.Symbol != null && t.Symbol != "")
            .GroupBy(t => t.Symbol!)
            .Select(g => new TradedSymbol
            {
                Symbol = g.Key,
                LastTradeDate = g.Max(t => t.PublishingDate)
            })
            .OrderBy(s => s.Symbol)
            .ToListAsync();

        cache.Set(cacheKey, symbols, TimeSpan.FromHours(6));
        return symbols;
    }

    public async Task<int> BackfillTradeDetailsAsync(List<InsiderTrade> freshTrades)
    {
        if (freshTrades.Count == 0)
            return 0;

        var dates = freshTrades.Select(t => t.PublishingDate).Distinct().ToList();
        var existing = await context.InsiderTrades
            .Where(t => dates.Contains(t.PublishingDate))
            .ToListAsync();

        var updated = 0;
        foreach (var fresh in freshTrades.Where(t => !t.IsRevised()))
        {
            var row = existing.FindDuplicate(fresh);
            if (row is null)
                continue;

            var changed = false;
            if (row.Lei is null && fresh.Lei is not null) { row.Lei = fresh.Lei; changed = true; }
            if (row.InstrumentType is null && fresh.InstrumentType is not null) { row.InstrumentType = fresh.InstrumentType; changed = true; }
            if (row.TradingVenue is null && fresh.TradingVenue is not null) { row.TradingVenue = fresh.TradingVenue; changed = true; }
            if (row.IsCloselyAssociated is null && fresh.IsCloselyAssociated is not null) { row.IsCloselyAssociated = fresh.IsCloselyAssociated; changed = true; }
            if (row.IsShareProgram is null && fresh.IsShareProgram is not null) { row.IsShareProgram = fresh.IsShareProgram; changed = true; }
            if (row.ValueSek is null && fresh.ValueSek is not null) { row.ValueSek = fresh.ValueSek; changed = true; }

            if (changed) updated++;
        }

        if (updated > 0)
            await context.SaveChangesAsync();

        return updated;
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
                 // Only SEK-denominated values are summed so foreign-currency trades
                 // aren't counted as kronor. ValueSek is null when no FX rate exists.
                 TotalValue = Math.Round(g.Sum(t => t.ValueSek ?? 0m), 2),
                 UniqueCompanies = g.Select(t => t.Symbol).Distinct().Count()
             })
             .FirstOrDefaultAsync();

        cache.Set(cacheKey, stats, new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromHours(6)));

        return stats ?? new YtdStats { TotalTransactions = 0, TotalValue = 0, UniqueCompanies = 0 };
    }
}