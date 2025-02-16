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

        foreach (var trade in insiderTrades)
        {
            var exists = await _context.InsiderTrades.AnyAsync(t =>
                t.CompanyName == trade.CompanyName &&
                t.InsiderName == trade.InsiderName &&
                t.Position == trade.Position &&
                t.TransactionType == trade.TransactionType &&
                t.Date == trade.Date);

            if (!exists)
            {
                _context.InsiderTrades.Add(trade);
            }
            else
            {
                return "Data already exists.";
            }
        }

        await _context.SaveChangesAsync();
        return "Data stored successfully!";
    }

    public async Task<IEnumerable<InsiderTrade>> GetInsiderTrades()
    {
        return await _context.InsiderTrades.ToListAsync();
    }
}
