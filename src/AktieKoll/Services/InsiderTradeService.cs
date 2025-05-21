using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Services;

public class InsiderTradeService : IInsiderTradeService
{
    private readonly ApplicationDbContext _context;

    public InsiderTradeService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> AddInsiderTrades(List<InsiderTrade> insiderTrades)
    {
        if (insiderTrades == null || insiderTrades.Count == 0)
        {
            return "No data provided.";
        }

        // Get distinct dates from the incoming trades.
        var dates = insiderTrades.Select(t => t.Date).Distinct().ToList();

        // Fetch existing trades from the DB for those dates.
        var existingTrades = await _context.InsiderTrades
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
                _context.InsiderTrades.Add(trade);
                newTradesCount++;
            }
        }

        if (newTradesCount > 0)
        {
            await _context.SaveChangesAsync();
            return $"{newTradesCount} new trades added.";
        }
        else
        {
            return "No new data was added.";
        }
    }


    public async Task<IEnumerable<InsiderTrade>> GetInsiderTrades()
    {
        return await _context.InsiderTrades
            .OrderByDescending(id => id)
            .ToListAsync();
    }
}
