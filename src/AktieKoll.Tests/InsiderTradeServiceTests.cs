using AktieKoll.Data;
using AktieKoll.Models;
using AktieKoll.Services;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Tests;

//public class InsiderTradeServiceTests
//{
//    private ApplicationDbContext CreateContext()
//    {
//        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
//            .UseInMemoryDatabase(Guid.NewGuid().ToString())
//            .Options;
//        return new ApplicationDbContext(opts);
//    }

//    [Fact]
//    public async Task AddInsiderTrades_NewList_AddsTrades()
//    {
//        // Arrange
//        var ctx = CreateContext();
//        var service = new InsiderTradeService(ctx);
//        var trades = new List<InsiderTrade>
//        {
//            new InsiderTrade {
//                CompanyName = "FooCorp",
//                InsiderName = "Alice",
//                Position = "CFO",
//                TransactionType = "Buy",
//                Date = DateTime.Today
//            }
//        };

//        // Act
//        var result = await service.AddInsiderTrades(trades);

//        // Assert
//        Assert.Equal("1 new trades added.", result);
//        var saved = await ctx.InsiderTrades.ToListAsync();
//        Assert.Single(saved);
//    }
//}
