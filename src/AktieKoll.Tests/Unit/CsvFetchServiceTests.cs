using AktieKoll.Services;
using AktieKoll.Models;
using static AktieKoll.Models.CsvDtoExtensions;

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
}