using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AktieKoll.Services;

public class InsiderTradeService(ApplicationDbContext context) : IInsiderTradeService
{

    public async Task<string> AddInsiderTrades(List<InsiderTrade> insiderTrades)
    {
        if (insiderTrades == null || insiderTrades.Count == 0)
        {
            return "No data provided.";
        }

        // Get distinct dates from the incoming trades.
        var dates = insiderTrades.Select(t => t.Date).Distinct().ToList();

        // Fetch existing trades from the DB for those dates.
        var existingTrades = await context.InsiderTrades
            .Where(t => dates.Contains(t.Date))
            .ToListAsync();

        int newTradesCount = 0;
        foreach (var trade in insiderTrades)
        {
            // Check if a trade with the same composite key exists.
            bool exists = existingTrades.Any(t =>
                t.CompanyName == trade.CompanyName &&
                t.InsiderName == trade.InsiderName &&
                t.Position == trade.Position &&
                t.TransactionType == trade.TransactionType &&
                t.Date == trade.Date);

            if (exists)
            {
                // Assuming that once a duplicate is found, the remaining trades are duplicates.
                break;
            }
            else
            {
                context.InsiderTrades.Add(trade);
                newTradesCount++;
            }
        }

        if (newTradesCount > 0)
        {
            await context.SaveChangesAsync();
            return $"{newTradesCount} new trades added.";
        }
        else
        {
            return "No new data was added.";
        }
    }


    public async Task<IEnumerable<InsiderTrade>> GetInsiderTrades()
    {
        return await context.InsiderTrades
            .OrderByDescending(t => t.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<InsiderTrade>> GetInsiderTradesTop()
    {
        var today = DateTime.Now.Date;
        var yesterday = today.AddDays(-1);
        var tomorrow = today.AddDays(1);

        return await context.InsiderTrades
            //.Where(t => t.Date >= yesterday && t.Date < tomorrow)
            .OrderByDescending(t => t.Price * t.Shares)
            .Take(10)
            .ToListAsync();
    }
}
