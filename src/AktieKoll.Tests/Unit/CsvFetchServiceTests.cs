using AktieKoll.Services;
using Moq;
using Moq.Protected;
using System.Net;

namespace AktieKoll.Tests.Unit;

public class CsvFetchServiceTests
{
    [Fact]
    public async Task FetchInsiderTradesAsync_ReturnsParsedTrades()
    {
        // Arrange
        var csv = "CompanyName,InsiderName,Position,TransactionType,Shares,Price,Date\r\n" +
                  "FooCorp,Alice,CFO,Buy,100,10.5,2024-06-01\r\n" +
                  "BarCorp,Bob,CEO,Sell,200,20.0,2024-06-02\r\n";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(csv),
            });

        var httpClient = new HttpClient(handlerMock.Object);

        // Use the real CsvHelper.CsvReader for parsing
        var service = new CsvFetchService(httpClient);

        // Act
        var result = await service.FetchInsiderTradesAsync(csvReaderFactory);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("FooCorp", result[0].CompanyName);
        Assert.Equal("BarCorp", result[1].CompanyName);
        Assert.Equal("Alice", result[0].InsiderName);
        Assert.Equal("Bob", result[1].InsiderName);
    }
}
