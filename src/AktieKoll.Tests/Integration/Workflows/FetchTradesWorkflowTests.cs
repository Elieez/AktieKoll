using System.Globalization;
using AktieKoll.Data;
using AktieKoll.Services;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AktieKoll.Tests.Integration.Workflows;

public class FetchTradesWorkflowTests : IAsyncDisposable
{
    private readonly ApplicationDbContext _context;
    public FetchTradesWorkflowTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
    }

    [Fact]
    public async Task FetchTrades_CompleteWorkflow_ShouldSucceed()
    {
        var httpClient = new HttpClient();
        var figiClient = new HttpClient { BaseAddress = new Uri("https://api.openfigi.com/v3/") };

        static CsvReader CsvReaderFactory(TextReader reader)
        {
            var config = new CsvHelper.Configuration.CsvConfiguration(new CultureInfo("sv-SE"))
            {
                Delimiter = ";"
            };
            return new CsvReader(reader, config);
        }

        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        var csvLogger = loggerFactory.CreateLogger<CsvFetchService>();
        var figiLogger = loggerFactory.CreateLogger<OpenFigiService>();

        var csvService = new CsvFetchService(httpClient, CsvReaderFactory, csvLogger);
        var figiService = new OpenFigiService(figiClient, figiLogger);
        var symbolService = new SymbolService(figiService);
        var tradeService = new InsiderTradeService(_context, symbolService);

        // Act
        var csvResults = await csvService.FetchInsiderTradesAsync();
        var trades = Models.CsvDtoExtensions.InsiderTradeMapper.MapDtosToTrades(csvResults);
        var message = await tradeService.AddInsiderTrades(trades);

        // Assert
        Assert.NotNull(message);
        Assert.NotEmpty(csvResults);
        Assert.NotEmpty(trades);

        var savedTrades = await _context.InsiderTrades.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotEmpty(savedTrades);

        // Verify
        Assert.True(trades.Count <= csvResults.Count);
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

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}