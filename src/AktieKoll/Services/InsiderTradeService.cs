using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;

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
            bool exists = existingTrades.Any(t =>
                t.CompanyName == trade.CompanyName &&
                t.InsiderName == trade.InsiderName &&
                t.Position == trade.Position &&
                t.TransactionType == trade.TransactionType &&
                t.PublishingDate == trade.PublishingDate
                );

            bool isRevised = string.Equals(trade.Status, "Reviderad", StringComparison.OrdinalIgnoreCase);
            if (isRevised)
            {
                var toRemove = existingTrades.FirstOrDefault(t => 
                    t.CompanyName == trade.CompanyName &&
                    t.InsiderName == trade.InsiderName &&
                    t.Position == trade.Position &&
                    t.TransactionType == trade.TransactionType &&
                    t.PublishingDate == trade.PublishingDate);

                if (toRemove != null)
                {
                    context.InsiderTrades.Remove(toRemove);
                    existingTrades.Remove(toRemove);
                    removedTradesCount++;
                }
                continue;
            }

            if (exists)
            {
                break;
            }
            else
            {
                context.InsiderTrades.Add(trade);
                existingTrades.Add(trade);
                newTradesCount++;
            }
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
}
