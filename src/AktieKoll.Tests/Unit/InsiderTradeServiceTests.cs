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
        Assert.Equal("1 new trades added.", result.Message);
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

        Assert.Equal("No new data was added.", result.Message);
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

        Assert.Equal("1 trades removed.", result.Message);
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

        Assert.Equal("No new data was added.", result.Message);
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

        var result = await service.GetInsiderTradesPage(1, 100);

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

    [Fact]
    public async Task BackfillTradeDetails_OldRowWithRawPosition_IsMatchedAndUpdated()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var published = new DateTime(2025, 6, 17, 22, 59, 32, DateTimeKind.Utc);

        // Stored before position cleanup: raw FI role with non-breaking spaces, old company name.
        ctx.InsiderTrades.Add(new InsiderTrade
        {
            CompanyName = "Eltel AB",
            InsiderName = "Håkan Dahlström",
            Position = "Verkställande direktör (VD)",
            TransactionType = "Förvärv",
            Shares = 33537,
            Price = 8.99m,
            Currency = "SEK",
            Status = "Aktuell",
            PublishingDate = published,
            TransactionDate = published.Date
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);
        var fresh = new InsiderTrade
        {
            CompanyName = "Eltel",
            InsiderName = "Håkan Dahlström",
            Position = "VD",
            TransactionType = "Förvärv",
            Shares = 33537,
            Price = 8.99m,
            Currency = "SEK",
            Status = "Aktuell",
            PublishingDate = published,
            TransactionDate = published.Date,
            InstrumentType = "Aktie",
            IsShareProgram = false,
            IsCloselyAssociated = false,
            ValueSek = 301497.63m
        };

        var result = await service.BackfillTradeDetailsAsync([fresh]);

        Assert.Equal(new BackfillResult(1, 1, 1), result);
        var row = await ctx.InsiderTrades.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Aktie", row.InstrumentType);
        Assert.False(row.IsShareProgram);
        Assert.Equal("VD", row.Position);
    }

    [Fact]
    public async Task BackfillTradeDetails_TradeNotInDb_IsNotInserted()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);
        var published = new DateTime(2025, 6, 19, 17, 58, 55, DateTimeKind.Utc);

        var result = await service.BackfillTradeDetailsAsync([new InsiderTrade
        {
            CompanyName = "Eltel",
            InsiderName = "Håkan Dahlström",
            Position = "VD",
            TransactionType = "Förvärv",
            Shares = 1837,
            Price = 9.40m,
            Currency = "SEK",
            Status = "Aktuell",
            PublishingDate = published,
            TransactionDate = published.Date
        }]);

        Assert.Equal(new BackfillResult(1, 0, 0), result);
        Assert.Empty(await ctx.InsiderTrades.ToListAsync(TestContext.Current.CancellationToken));
    }
}