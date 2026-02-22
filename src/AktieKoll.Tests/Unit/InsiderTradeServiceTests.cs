using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;
using AktieKoll.Tests.Shared.TestHelpers;

namespace AktieKoll.Tests.Unit;

public class InsiderTradeServiceTests
{
    [Fact]
    public async Task AddInsiderTrades_NewList_AddsTrades()
    {
        // Arrange
        var ctx = ServiceTestHelpers.CreateContext();
        await ServiceTestHelpers.SeedCompanies(ctx, ("SE001", "FOO"));

        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);
        var trades = new List<InsiderTrade>
        {
            new() {
                CompanyName = "FooCorp",
                InsiderName = "Alice",
                Position = "CFO",
                TransactionType = "Buy",
                Shares = 100,
                Price = 10.5m,
                Currency = "SEK",
                Status = "Aktuell",
                Isin = "SE001",
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
        Assert.Equal("FOO", saved[0].Symbol);
    }

    [Fact]
    public async Task AddInsiderTrades_Duplicate_DoesNotAdd()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        await ServiceTestHelpers.SeedCompanies(ctx, ("SE001", "FOO"));

        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);
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
            Isin = "SE001",
            PublishingDate = DateTime.Today,
            TransactionDate = DateTime.Today
        };
        ctx.InsiderTrades.Add(trade);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await service.AddInsiderTrades([trade]);

        Assert.Equal("No new data was added.", result);
        var count = await ctx.InsiderTrades.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task AddInsiderTrades_RevisedEntry_RemovesExisting()
    {
        var ctx = ServiceTestHelpers.CreateContext();
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

        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);
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
        var ctx = ServiceTestHelpers.CreateContext();
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

        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);
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
        var ctx = ServiceTestHelpers.CreateContext();
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
        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);

        var result = await service.GetInsiderTrades();

        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task AddInsiderTrades_ResolvesIsinToSymbol()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        await ServiceTestHelpers.SeedCompanies(ctx, ("ISIN123", "TICK"));

        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);
        var trades = new List<InsiderTrade>
        {
            new() {
                CompanyName = "FooCorp",
                InsiderName = "Alice",
                Position = "CFO",
                TransactionType = "Buy",
                Shares = 10,
                Price = 10m,
                Currency = "SEK",
                Status = "Aktuell",
                PublishingDate = DateTime.Today,
                TransactionDate = DateTime.Today,
                Isin = "ISIN123"
            }
        };

        await service.AddInsiderTrades(trades);

        var saved = await ctx.InsiderTrades.FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("TICK", saved.Symbol);
    }
}