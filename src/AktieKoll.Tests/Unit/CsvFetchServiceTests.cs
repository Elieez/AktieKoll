using AktieKoll.Models;
using AktieKoll.Services;
using CsvHelper;
using Moq;
using Moq.Protected;
using System.Globalization;
using System.Net;

namespace AktieKoll.Tests.Unit;

public class CsvFetchServiceTests
{

    [Fact]
    public async Task FetchInsiderTradesAsync_ReturnsParsedCsvDTOs()
    {
        // Arrange: CSV header and two rows, matching CsvDTO (semicolon-delimited)
        var csv =
    "Publiceringsdatum;Emittent;LEI-kod;Anmälningsskyldig;Person i ledande ställning;Befattning;Närstående;Korrigering;Beskrivning av korrigering;Är förstagångsrapportering;Är kopplad till aktieprogram;Karaktär;Instrumenttyp;Instrumentnamn;ISIN;Transaktionsdatum;Volym;Volymenhet;Pris;Valuta;Handelsplats;Status\r\n" +
    "2024-06-01 13:37:17;TestCorp;1234567890ABCDEF;Anna Andersson;Anna Andersson;VD;Nej;Nej;;Nej;Nej;Köp;Aktie;TestAktie;SE0000000001;2024-05-31 00:00:00;100;st;123,45;SEK;XSTO;Publicerad\r\n" +
    "2024-06-02 13:37:17;DemoCorp;0987654321ZYXWVU;Bertil Berg;Bertil Berg;CFO;Ja;Ja;;Ja;Ja;Sälj;Aktie;DemoAktie;SE0000000002;2024-06-01 00:00:00;200;st;234,56;SEK;XNGM;Publicerad\r\n";

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

        // Factory that configures CsvHelper to use semicolon as delimiter
        static CsvReader csvReaderFactory(TextReader reader)
        {
            var config = new CsvHelper.Configuration.CsvConfiguration(new CultureInfo("sv-SE"))
            {
                Delimiter = ";"
            };
            return new CsvReader(reader, config);
        }

        // You may need to adjust CsvFetchService to return List<CsvDTO> for this test
        var service = new CsvFetchService(httpClient, csvReaderFactory);

        // Act
        var result = await service.FetchInsiderTradesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        // Check a few fields for the first row
        Assert.Equal("TestCorp", result[0].Emittent);
        Assert.Equal("Anna Andersson", result[0].Anmälningsskyldig);
        Assert.Equal("VD", result[0].Befattning);
        Assert.Equal("SE0000000001", result[0].ISIN);
        Assert.Equal(100, result[0].Volym);
        Assert.Equal(123.45m, result[0].Pris);

        // Check a few fields for the second row
        Assert.Equal("DemoCorp", result[1].Emittent);
        Assert.Equal("Bertil Berg", result[1].Anmälningsskyldig);
        Assert.Equal("CFO", result[1].Befattning);
        Assert.Equal("SE0000000002", result[1].ISIN);
        Assert.Equal(200, result[1].Volym);
        Assert.Equal(234.56m, result[1].Pris);
    }
}
