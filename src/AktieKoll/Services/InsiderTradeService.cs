using AktieKoll.Data;
using AktieKoll.Extensions;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileSystemGlobbing.Internal.PatternContexts;

namespace AktieKoll.Services;

public class InsiderTradeService(ApplicationDbContext context) : IInsiderTradeService
{
    public async Task<string> AddInsiderTrades(List<InsiderTrade> insiderTrades)
    {
        if (insiderTrades == null || insiderTrades.Count == 0)
        {
            return "No data provided.";
        }

        var dates = insiderTrades.Select(t => t.PublishingDate).Distinct().ToList();

        var existingTrades = await context.InsiderTrades
            .Where(t => dates.Contains(t.PublishingDate))
            .ToListAsync();

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

            context.InsiderTrades.Add(trade);
            existingTrades.Add(trade);
            newTradesCount++;
        }

        if (newTradesCount > 0 || removedTradesCount > 0)
        {
            await context.SaveChangesAsync();
            
            if (newTradesCount > 0 && removedTradesCount > 0)
            {
                return $"{newTradesCount} new trades added. {removedTradesCount} trades removed.";
            }
            if (newTradesCount > 0)
            {
                return $"{newTradesCount} new trades added.";
            }
            return $"{removedTradesCount} trades removed.";
        }
        return "No new data was added.";
               
    }

    public async Task<IEnumerable<InsiderTrade>> GetInsiderTrades()
    {
        return await context.InsiderTrades
            .OrderByDescending(t => t.PublishingDate)
            .ToListAsync();
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
        var today = DateTime.Now.Date;
        var yesterday = today.AddDays(-1);
        var tomorrow = today.AddDays(1);

        return await context.InsiderTrades
            .Where(t => t.PublishingDate >= yesterday && t.PublishingDate < tomorrow)
            .OrderByDescending(t => t.Price * t.Shares)
            .Take(10)
            .ToListAsync();
    }

    private async Task<IEnumerable<CompanyTransactionStats>> GetTransactionCountByType(string transactionType, string? companyName, int days, int? top)
    {
        var endDate = DateTime.Now.Date.AddDays(1);
        var startDate = endDate.AddDays(-days);

        var query = context.InsiderTrades
            .Where(t => t.PublishingDate >= startDate && t.PublishingDate < endDate)
            .Where(t => t.TransactionType.ToLower() == transactionType.ToLower());

        if (!string.IsNullOrWhiteSpace(companyName))
        {
            var filtered = companyName.FilterCompanyName();
            query = query.Where(t => t.CompanyName.ToLower() == filtered.ToLower());
        }

        var grouped = query
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
            grouped = (IOrderedQueryable<CompanyTransactionStats>)grouped.Take(top.Value);
        }

        return await grouped.ToListAsync();
    }

    public Task<IEnumerable<CompanyTransactionStats>> GetTransactionCountBuy(string? companyName, int days = 30, int? top = 5)
        => GetTransactionCountByType("Förvärv", companyName, days, top);
    public Task<IEnumerable<CompanyTransactionStats>> GetTransactionCountSell(string? companyName, int days = 30, int? top = 5)
        => GetTransactionCountByType("Avyttring", companyName, days, top);

    public async Task<IEnumerable<InsiderTrade>> GetInsiderTradesByCompany(string companyName, int skip = 0, int take = 10)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            return [];
        }

        var filteredCompanyName = companyName.FilterCompanyName();

        return await context.InsiderTrades
            .Where(t => t.CompanyName.ToLower() == filteredCompanyName.ToLower())
            .OrderByDescending(t => t.PublishingDate)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }
}
