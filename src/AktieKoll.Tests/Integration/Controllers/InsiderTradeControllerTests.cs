using System.Net;
using AktieKoll.Data;
using AktieKoll.Dtos;
using AktieKoll.Models;
using AktieKoll.Tests.Fixture;
using AktieKoll.Tests.Integration.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AktieKoll.Tests.Integration.Controllers;

public class InsiderTradeControllerTests(WebApplicationFactoryFixture factory) : IntegrationTestBase(factory)
{
    private async Task SeedTradesAsync(params InsiderTrade[] trades)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.InsiderTrades.AddRange(trades);
        await db.SaveChangesAsync(Token);
    }

    private static InsiderTrade MakeTrade(
        string companyName = "TestCorp",
        string insiderName = "John Doe",
        string transactionType = "Förvärv",
        int shares = 100,
        decimal price = 50m,
        DateTime? publishingDate = null) => new()
        {
            CompanyName = companyName,
            InsiderName = insiderName,
            Position = "CEO",
            TransactionType = transactionType,
            Shares = shares,
            Price = price,
            Currency = "SEK",
            Status = "Aktuell",
            PublishingDate = publishingDate ?? DateTime.Today,
            TransactionDate = publishingDate ?? DateTime.Today
        };

    // GET /api/insidertrade/page
    [Fact]
    public async Task GetInsiderTradesPage_DefaultParams_ReturnsFirstPage()
    {
        // Arrange
        await SeedTradesAsync(
            MakeTrade("Corp1"),
            MakeTrade("Corp2"),
            MakeTrade("Corp3")
        );

        // Act
        var response = await Client.GetTestAsync("/api/insidertrades/page");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var trades = await response.Content.ReadFromJsonTestAsync<List<InsiderTradeListDto>>();
        trades.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetInsiderTradesPage_WithPageSize_ReturnsCorrectCount()
    {
        // Arrange
        await SeedTradesAsync(
            MakeTrade("Corp1"),
            MakeTrade("Corp2"),
            MakeTrade("Corp3"),
            MakeTrade("Corp4"),
            MakeTrade("Corp5")
        );

        // Act
        var response = await Client.GetTestAsync("/api/insidertrades/page?page=1&pageSize=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var trades = await response.Content.ReadFromJsonTestAsync<List<InsiderTradeListDto>>();
        trades.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetInsiderTradesPage_SecondPage_ReturnsDifferentTrades()
    {
        // Arrange
        await SeedTradesAsync(
            MakeTrade("Corp1"),
            MakeTrade("Corp2"),
            MakeTrade("Corp3"),
            MakeTrade("Corp4")
        );

        // Act
        var page1 = await Client.GetTestAsync("/api/insidertrades/page?page=1&pageSize=2");
        var page2 = await Client.GetTestAsync("/api/insidertrades/page?page=2&pageSize=2");

        // Assert
        var trades1 = await page1.Content.ReadFromJsonTestAsync<List<InsiderTradeListDto>>();
        var trades2 = await page2.Content.ReadFromJsonTestAsync<List<InsiderTradeListDto>>();

        trades1.Should().HaveCount(2);
        trades2.Should().HaveCount(2);
        trades1![0].CompanyName.Should().NotBe(trades2![0].CompanyName);
    }

    [Fact]
    public async Task GetInsiderTradesPage_InvalidPage_ReturnsBadRequest()
    {
        // Act
        var response = await Client.GetTestAsync("/api/insidertrades/page?page=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetInsiderTradesPage_InvalidPageSize_ReturnsBadRequest()
    {
        // Act
        var response = await Client.GetTestAsync("/api/insidertrades/page?page=1&pageSize=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // GET /api/insidertrade/top
    [Fact]
    public async Task GetInsiderTradesTop_EmptyDatabase_ReturnsOkWithEmptyArray()
    {
        // Act
        var response = await Client.GetTestAsync("/api/insidertrades/top");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var trades = await response.Content.ReadFromJsonTestAsync<List<InsiderTrade>>();
        trades.Should().BeEmpty();
    }

    [Fact]
    public async Task GetInsiderTradesTop_ReturnsSortedByValue()
    {
        // Arrange
        await SeedTradesAsync(
            MakeTrade("SmallCorp", shares: 10, price: 1m),    // Value: 10
            MakeTrade("BigCorp", shares: 1000, price: 100m)   // Value: 100,000
        );

        // Act
        var response = await Client.GetTestAsync("/api/insidertrades/top");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var trades = await response.Content.ReadFromJsonTestAsync<List<InsiderTradeListDto>>();
        trades.Should().NotBeEmpty();
        trades![0].CompanyName.Should().Be("BigCorp");
    }

    // GET /api/insidertrade/count-buy
    [Fact]
    public async Task GetTransactionCountBuy_EmptyDatabase_ReturnsOkWithEmptyArray()
    {
        // Act
        var response = await Client.GetTestAsync("/api/insidertrades/count-buy");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stats = await response.Content.ReadFromJsonTestAsync<List<CompanyTransactionStats>>();
        stats.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTransactionCountBuy_WithTrades_ReturnsStats()
    {
        // Arrange
        await SeedTradesAsync(
            MakeTrade("Volvo", transactionType: "Förvärv"),
            MakeTrade("Volvo", transactionType: "Förvärv"),
            MakeTrade("Ericsson", transactionType: "Förvärv")
        );

        // Act
        var response = await Client.GetTestAsync("/api/insidertrades/count-buy?days=365");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stats = await response.Content.ReadFromJsonTestAsync<List<CompanyTransactionStats>>();
        stats.Should().NotBeEmpty();

        var volvoStats = stats!.FirstOrDefault(s => s.CompanyName == "Volvo");
        volvoStats.Should().NotBeNull();
        volvoStats!.TransactionCount.Should().Be(2);
    }

    [Fact]
    public async Task GetTransactionCountBuy_FilterByCompany_ReturnsOnlyThatCompany()
    {
        // Arrange
        await SeedTradesAsync(
            MakeTrade("Volvo", transactionType: "Förvärv"),
            MakeTrade("Ericsson", transactionType: "Förvärv")
        );

        // Act
        var response = await Client.GetTestAsync("/api/insidertrades/count-buy?companyName=Volvo&days=365");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stats = await response.Content.ReadFromJsonTestAsync<List<CompanyTransactionStats>>();
        stats.Should().HaveCount(1);
        stats![0].CompanyName.Should().Be("Volvo");
    }

    [Fact]
    public async Task GetTransactionCountBuy_WithTopFilter_ReturnsLimitedResults()
    {
        // Arrange
        await SeedTradesAsync(
            MakeTrade("Corp1", transactionType: "Förvärv"),
            MakeTrade("Corp2", transactionType: "Förvärv"),
            MakeTrade("Corp3", transactionType: "Förvärv"),
            MakeTrade("Corp4", transactionType: "Förvärv"),
            MakeTrade("Corp5", transactionType: "Förvärv")
        );

        // Act
        var response = await Client.GetTestAsync("/api/insidertrades/count-buy?days=365&top=3");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stats = await response.Content.ReadFromJsonTestAsync<List<CompanyTransactionStats>>();
        stats.Should().HaveCount(3);
    }

    // GET /api/insidertrade/count-sell
    [Fact]
    public async Task GetTransactionCountSell_EmptyDatabase_ReturnsOkWithEmptyArray()
    {
        // Act
        var response = await Client.GetTestAsync("/api/insidertrades/count-sell");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stats = await response.Content.ReadFromJsonTestAsync<List<CompanyTransactionStats>>();
        stats.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTransactionCountSell_WithTrades_ReturnsStats()
    {
        // Arrange
        await SeedTradesAsync(
            MakeTrade("Volvo", transactionType: "Avyttring"),
            MakeTrade("Volvo", transactionType: "Avyttring"),
            MakeTrade("Ericsson", transactionType: "Avyttring")
        );

        // Act
        var response = await Client.GetTestAsync("/api/insidertrades/count-sell?days=365");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stats = await response.Content.ReadFromJsonTestAsync<List<CompanyTransactionStats>>();
        stats.Should().NotBeEmpty();
    }

    // GET /api/insidertrade/company
    [Fact]
    public async Task GetByCompanyName_ValidCompany_ReturnsTrades()
    {
        // Arrange
        await SeedTradesAsync(
            MakeTrade("Volvo"),
            MakeTrade("Ericsson") // Should NOT be returned
        );

        // Act
        var response = await Client.GetTestAsync("/api/insidertrades/company?name=Volvo");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var trades = await response.Content.ReadFromJsonTestAsync<List<InsiderTradeListDto>>();
        trades.Should().HaveCount(1);
        trades![0].CompanyName.Should().Be("Volvo");
    }

    [Fact]
    public async Task GetByCompanyName_NotFound_ReturnsNotFound()
    {
        // Act
        var response = await Client.GetTestAsync("/api/insidertrades/company?name=NonExistent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByCompanyName_WithPagination_ReturnsCorrectCount()
    {
        // Arrange - seed 5 trades for same company
        await SeedTradesAsync(
            MakeTrade("Volvo", publishingDate: DateTime.Today),
            MakeTrade("Volvo", publishingDate: DateTime.Today.AddDays(-1)),
            MakeTrade("Volvo", publishingDate: DateTime.Today.AddDays(-2)),
            MakeTrade("Volvo", publishingDate: DateTime.Today.AddDays(-3)),
            MakeTrade("Volvo", publishingDate: DateTime.Today.AddDays(-4))
        );

        // Act - take only 2
        var response = await Client.GetTestAsync("/api/insidertrades/company?name=Volvo&skip=0&take=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var trades = await response.Content.ReadFromJsonTestAsync<List<InsiderTradeListDto>>();
        trades.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByCompanyName_WithSkip_ReturnsCorrectPage()
    {
        // Arrange
        await SeedTradesAsync(
            MakeTrade("Volvo", publishingDate: DateTime.Today),
            MakeTrade("Volvo", publishingDate: DateTime.Today.AddDays(-1)),
            MakeTrade("Volvo", publishingDate: DateTime.Today.AddDays(-2)),
            MakeTrade("Volvo", publishingDate: DateTime.Today.AddDays(-3))
        );

        // Act
        var page1 = await Client.GetTestAsync("/api/insidertrades/company?name=Volvo&skip=0&take=2");
        var page2 = await Client.GetTestAsync("/api/insidertrades/company?name=Volvo&skip=2&take=2");

        // Assert
        var trades1 = await page1.Content.ReadFromJsonTestAsync<List<InsiderTradeListDto>>();
        var trades2 = await page2.Content.ReadFromJsonTestAsync<List<InsiderTradeListDto>>();

        trades1.Should().HaveCount(2);
        trades2.Should().HaveCount(2);
        trades1![0].PublishingDate.Should().NotBe(trades2![0].PublishingDate);
    }

    [Fact]
    public async Task GetByCompanyName_MissingNameParam_ReturnsBadRequest()
    {
        // Act - no name query param
        var response = await Client.GetTestAsync("/api/insidertrades/company");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // GET /api/insidertrades/ytd-stats
    [Fact]
    public async Task GetYtdStats_WithCurrentYearTrades_ReturnsCorrectCount()
    {
        // Arrange
        await SeedTradesAsync(
            MakeTrade("Volvo"),
            MakeTrade("Ericsson"),
            MakeTrade("Volvo")
        );

        // Act
        var response = await Client.GetTestAsync("/api/insidertrades/ytd-stats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stats = await response.Content.ReadFromJsonTestAsync<YtdStats>();
        stats!.TotalTransactions.Should().Be(3);
    }
}
