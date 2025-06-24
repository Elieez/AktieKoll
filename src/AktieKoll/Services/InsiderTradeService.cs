using AktieKoll.Data;
using AktieKoll.Extensions;
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
