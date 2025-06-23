using AktieKoll.Data;
using AktieKoll.Models;
using AktieKoll.Services;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Tests.Unit;

public class TransactionsDbTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetInsiderTrades_ReturnsAddedTrades()
    {
        var ctx = CreateContext();
        var service = new InsiderTradeService(ctx);

        var trades = new List<InsiderTrade>
        {
            new()
            {
                CompanyName = "FooCorp",
                InsiderName = "Alice",
                Position = "CFO",
                TransactionType = "Buy",
                Shares = 100,
                Price = 10.5m,
                Currency = "SEK",
                Status = "Aktuell",
                PublishingDate = DateTime.Today,
                TransactionDate = DateTime.Today
            },
            new()
            {
                CompanyName = "BarCorp",
                InsiderName = "Bob",
                Position = "CEO",
                TransactionType = "Sell",
                Shares = 200,
                Price = 20.0m,
                Currency = "SEK",
                Status = "Aktuell",
                PublishingDate = DateTime.Today.AddDays(-1),
                TransactionDate = DateTime.Today.AddDays(-1)
            }
        };

        await service.AddInsiderTrades(trades);

        var result = await service.GetInsiderTrades();

        await Verify(result);
    }

    [Fact]
    public async Task GetInsiderTradesTop_ReturnsTopByValue()
    {
        var ctx = CreateContext();
        var service = new InsiderTradeService(ctx);

        var trades = Enumerable.Range(0, 3).Select(i => new InsiderTrade
        {
            CompanyName = $"Corp{i}",
            InsiderName = $"Name{i}",
            Position = "Exec",
            TransactionType = "Buy",
            Shares = 100 * (i + 1),
            Price = 10m * (i + 1),
            Currency = "SEK",
            Status = "Aktuell",
            PublishingDate = DateTime.Today,
            TransactionDate = DateTime.Today
        }).ToList();

        await service.AddInsiderTrades(trades);

        var result = await service.GetInsiderTradesTop();

        await Verify(result);
    }

    [Fact]
    public async Task AddInsiderTrades_Duplicate()
    {
        var ctx = CreateContext();
        var service = new InsiderTradeService(ctx);

        var trades = new List<InsiderTrade>
        {
            new()
            {
                CompanyName = "FooCorp",
                InsiderName = "Alice",
                Position = "CFO",
                TransactionType = "Buy",
                Shares = 100,
                Price = 10.5m,
                Currency = "SEK",
                Status = "Aktuell",
                PublishingDate = DateTime.Today,
                TransactionDate = DateTime.Today
            },
            new()
            {
                CompanyName = "FooCorp",
                InsiderName = "Alice",
                Position = "CFO",
                TransactionType = "Buy",
                Shares = 100,
                Price = 10.5m,
                Currency = "SEK",
                Status = "Aktuell",
                PublishingDate = DateTime.Today,
                TransactionDate = DateTime.Today
            },
            new()
            {
                CompanyName = "BarCorp",
                InsiderName = "Bob",
                Position = "CEO",
                TransactionType = "Sell",
                Shares = 200,
                Price = 20.0m,
                Currency = "SEK",
                Status = "Aktuell",
                PublishingDate = DateTime.Today.AddDays(-1),
                TransactionDate = DateTime.Today.AddDays(-1)
            }
        };

        await service.AddInsiderTrades(trades);

        var result = await service.GetInsiderTrades();

        await Verify(result);
    }
}
