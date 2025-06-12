using AktieKoll.Data;
using AktieKoll.Models;
using AktieKoll.Services;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Tests;

public class InsiderTradeServiceTests
{
    private ApplicationDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(opts);
    }

    [Fact]
    public async Task AddInsiderTrades_NewList_AddsTrades()
    {
        // Arrange
        var ctx = CreateContext();
        var service = new InsiderTradeService(ctx);
        var trades = new List<InsiderTrade>
        {
            new InsiderTrade {
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
            }
        };

        // Act
        var result = await service.AddInsiderTrades(trades);

        // Assert
        Assert.Equal("1 new trades added.", result);
        var saved = await ctx.InsiderTrades.ToListAsync();
        Assert.Single(saved);
    }

    [Fact]
    public async Task AddInsiderTrades_Duplicate_DoesNotAdd()
    {
        var ctx = CreateContext();
        var service = new InsiderTradeService(ctx);
        var trade = new InsiderTrade
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
        };
        ctx.InsiderTrades.Add(trade);
        await ctx.SaveChangesAsync();

        var result = await service.AddInsiderTrades(new List<InsiderTrade> { trade });

        Assert.Equal("No new data was added.", result);
        var count = await ctx.InsiderTrades.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task AddInsiderTrades_StopAtFirstDuplicate()
    {
        var ctx = CreateContext();
        var service = new InsiderTradeService(ctx);

        var existing = new InsiderTrade
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
        };
        ctx.InsiderTrades.Add(existing);
        await ctx.SaveChangesAsync();

        var newTrade = new InsiderTrade
        {
            CompanyName = "BarCorp",
            InsiderName = "Bob",
            Position = "CEO",
            TransactionType = "Sell",
            Shares = 200,
            Price = 20.0m,
            Currency = "SEK",
            Status = "Aktuell",
            PublishingDate = DateTime.Today,
            TransactionDate = DateTime.Today
        };

        var laterTrade = new InsiderTrade
        {
            CompanyName = "BazCorp",
            InsiderName = "Cara",
            Position = "CIO",
            TransactionType = "Buy",
            Shares = 300,
            Price = 30.0m,
            Currency = "SEK",
            Status = "Aktuell",
            PublishingDate = DateTime.Today,
            TransactionDate = DateTime.Today
        };

        var trades = new List<InsiderTrade> { newTrade, existing, laterTrade };

        var result = await service.AddInsiderTrades(trades);

        Assert.Equal("1 new trades added.", result);
        var saved = await ctx.InsiderTrades.ToListAsync();
        Assert.Equal(2, saved.Count);
        Assert.Contains(saved, t => t.CompanyName == "BarCorp");
        Assert.DoesNotContain(saved, t => t.CompanyName == "BazCorp");
    }

    [Fact]
    public async Task GetInsiderTrades_ReturnsAllTrades()
    {
        var ctx = CreateContext();
        ctx.InsiderTrades.AddRange(
            new InsiderTrade
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
            new InsiderTrade
            {
                CompanyName = "BarCorp",
                InsiderName = "Bob",
                Position = "CEO",
                TransactionType = "Sell",
                Shares = 200,
                Price = 20.0m,
                Currency = "SEK",
                Status = "Aktuell",
                PublishingDate = DateTime.Today,
                TransactionDate = DateTime.Today
            }
        );
        await ctx.SaveChangesAsync();
        var service = new InsiderTradeService(ctx);

        var result = await service.GetInsiderTrades();

        Assert.Equal(2, result.Count());
    }
}
