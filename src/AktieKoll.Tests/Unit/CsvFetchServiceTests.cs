using AktieKoll.Services;
using AktieKoll.Tests.Shared.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using static AktieKoll.Extensions.CsvDtoExtensions;

namespace AktieKoll.Tests.Unit;

public class CsvFetchServiceTests
{
    [Theory]
    [InlineData("2025-01-01", "2025-01-02")]
    public async Task GetTransactionsRaw(DateTime fromDate, DateTime toDate)
    {
        // Given 
        var csvFetchService = ServiceProviderFixture
                                   .GetRequiredService<CsvFetchService>(services => services.AuthorizedClient());
        // When
        var result = await csvFetchService.FetchInsiderTradesAsync(fromDate, toDate);

        // Then
        await Verify(result);
    }

    [Theory]
    [InlineData("2025-01-01", "2025-01-02")]
    public async Task GetTransactionsFiltered(DateTime fromDate, DateTime toDate)
    {
        // Given 
        var csvFetchService = ServiceProviderFixture
                                   .GetRequiredService<CsvFetchService>(services => services.AuthorizedClient());
        // When
        var result = await csvFetchService.FetchInsiderTradesAsync(fromDate, toDate);
        var filteredResult = InsiderTradeMapper.MapDtosToTrades(result);

        // Then
        await Verify(filteredResult);
    }

    [Theory]
    [InlineData("2025-01-01", "2025-01-02")]
    public async Task GetTransactionsWithSymbolResolution(DateTime fromDate, DateTime toDate)
    {
        var ctx = ServiceTestHelpers.CreateContext();

        await ServiceTestHelpers.SeedCompanies(ctx,
            // Swedish stocks thats in the test data.
            ("SE0017131329", "LOGI-A", "Logistea A"),
            ("SE0017131337", "LOGI-B", "Logistea AB Series B"),
            ("SE0010323998", "BALCO", "Balco Group AB"),
            ("SE0000233934", "PRIC-B", "Pricer AB (publ)"),
            ("SE0017105620", "DYVOX", "Dynavox Group"),
            (null, "ENVAR", "Envar Holding AB"),
            ("SE0011167725", "ITECH", "I-Tech"),
            ("SE0000872095", "SOBI", "Swedish Orphan Biovitrum AB (publ)"),
            ("SE0000667925", "TELIA", "Telia Company AB"),
            ("SE0015245535", "NELLY", "Nelly Group AB"),
            ("SE0015988167", "SECARE", "Swedencare publ AB"),
            ("GB0009895292", "AZN", "AstraZeneca PLC"),
            ("SE0021309614", "ALIV-SDB", "Autoliv Inc"),
            ("SE0015658109", "EPI-A", "Epiroc AB (publ)"),
            ("SE0015658117", "EPI-B", "Epiroc AB (publ)")
        );

        
        var csvFetchService = ServiceProviderFixture
                                   .GetRequiredService<CsvFetchService>(services => services.AuthorizedClient());

        var logger = NullLogger<SymbolService>.Instance;
        var symbolService = new SymbolService(ctx, logger);

        var csvResult = await csvFetchService.FetchInsiderTradesAsync(fromDate, toDate);
        var trades = InsiderTradeMapper.MapDtosToTrades(csvResult);

        await symbolService.ResolveSymbolsAsync(trades);

        await Verify(new
        {
            TotalTrades = trades.Count,
            TradesWithSymbols = trades.Count(t => !string.IsNullOrEmpty(t.Symbol)),
            TradesWithoutSymbols = trades.Count(t => string.IsNullOrEmpty(t.Symbol)),
            trades
        });
    }
}