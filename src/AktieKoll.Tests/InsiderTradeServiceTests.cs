using AktieKoll.Data;
using AktieKoll.Models;
using AktieKoll.Services;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

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
        var saved = await ctx.InsiderTrades.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
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
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await service.AddInsiderTrades(new List<InsiderTrade> { trade });

        Assert.Equal("No new data was added.", result);
        var count = await ctx.InsiderTrades.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task AddInsiderTrades_RevisedEntry_RemovesExisting()
    {
        var ctx = CreateContext();
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
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new InsiderTradeService(ctx);
        var revisedTrade = new InsiderTrade
        {
            CompanyName = existing.CompanyName,
            InsiderName = existing.InsiderName,
            Position = existing.Position,
            TransactionType = existing.TransactionType,
            Shares = existing.Shares,
            Price = existing.Price,
            Currency = existing.Currency,
            Status = "Reviderad",
            PublishingDate = existing.PublishingDate,
            TransactionDate = existing.TransactionDate
        };

        var result = await service.AddInsiderTrades([revisedTrade]);

        Assert.Equal("1 trades removed.", result);
        var count = await ctx.InsiderTrades.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task AddInsiderTrades_RevisedEntry_NoMatch_NoChange()
    {
        var ctx = CreateContext();
        ctx.InsiderTrades.Add(new InsiderTrade
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
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new InsiderTradeService(ctx);
        var revised = new InsiderTrade
        {
            CompanyName = "BarCorp",
            InsiderName = "Bob",
            Position = "CEO",
            TransactionType = "Sell",
            Shares = 200,
            Price = 20.0m,
            Currency = "SEK",
            Status = "Reviderad",
            PublishingDate = DateTime.Today,
            TransactionDate = DateTime.Today
        };

        var result = await service.AddInsiderTrades([revised]);

        Assert.Equal("No new data was added.", result);
        var trades = await ctx.InsiderTrades.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(trades);
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
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new InsiderTradeService(ctx);

        var result = await service.GetInsiderTrades();

        Assert.Equal(2, result.Count());
    }
}
