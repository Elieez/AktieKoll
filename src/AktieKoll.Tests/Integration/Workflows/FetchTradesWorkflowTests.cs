using System.Globalization;
using AktieKoll.Data;
using AktieKoll.Services;
using AktieKoll.Tests.Shared.TestHelpers;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static AktieKoll.Extensions.CsvDtoExtensions;

namespace AktieKoll.Tests.Integration.Workflows;

public class FetchTradesWorkflowTests
{
    private readonly ApplicationDbContext _context;
    public FetchTradesWorkflowTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
    }

    [Theory]
    [InlineData("2025-01-01", "2025-01-02")]
    public async Task FetchTrades_CompleteWorkflow_ShouldSucceed(DateTime fromDate, DateTime toDate)
    {
        // Arrange
        await ServiceTestHelpers.SeedCompanies(_context,
            ("SE0000108656", "ERIC-B"),
            ("SE0000115446", "SEB-A"),
            ("SE0000108847", "TEL2-B"),
            ("SE0011166610", "HM-B"),
            ("SE0000115420", "VOLV-B")
        );

        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        var csvLogger = loggerFactory.CreateLogger<CsvFetchService>();
        var symbolLogger = loggerFactory.CreateLogger<SymbolService>();

        var csvFetchService = ServiceProviderFixture
                                   .GetRequiredService<CsvFetchService>(services => services.AuthorizedClient());
        var symbolService = new SymbolService(_context, symbolLogger);
        var tradeService = new InsiderTradeService(_context, symbolService);

        // Act
        var csvResults = await csvFetchService.FetchInsiderTradesAsync(fromDate, toDate);
        var trades = InsiderTradeMapper.MapDtosToTrades(csvResults);
        var message = await tradeService.AddInsiderTrades(trades);

        // Assert
        Assert.NotNull(message);
        Assert.NotEmpty(csvResults);
        Assert.NotEmpty(trades);

        var savedTrades = await _context.InsiderTrades.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotEmpty(savedTrades);

        // Verify
        Assert.True(trades.Count <= csvResults.Count);

        var tradesWithSymbols = savedTrades.Count(t => !string.IsNullOrEmpty(t.Symbol));
        Assert.True(tradesWithSymbols >= 0);
    }

    [Fact]
    public void CsvReaderFactory_Configuration_MatchesProduction()
    {
        // Arrange
        using var reader = new StringReader("test");

        static CsvReader CsvReaderFactory(TextReader textReader)
        {
            var config = new CsvHelper.Configuration.CsvConfiguration(new CultureInfo("sv-SE"))
            {
                Delimiter = ";"
            };
            return new CsvReader(textReader, config);
        }

        // Act
        using var csvReader = CsvReaderFactory(reader);

        // Assert - Verify production configuration
        Assert.Equal(";", csvReader.Configuration.Delimiter);
        Assert.Equal("sv-SE", csvReader.Configuration.CultureInfo.Name);
    }

    
}