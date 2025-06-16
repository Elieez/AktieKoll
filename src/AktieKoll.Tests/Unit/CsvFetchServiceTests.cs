using AktieKoll.Services;
using CsvHelper;
using Moq;
using Moq.Protected;
using System.Globalization;
using System.Net;
using System.Text;

namespace AktieKoll.Tests.Unit;

public class CsvFetchServiceTests
{

    //[Fact]
    //public async Task FetchInsiderTradesAsync_ReturnsParsedCsvDTOs()
    //{
    //    var csv =
    //            "Publiceringsdatum;Emittent;LEI-kod;Anmälningsskyldig;Person i ledande ställning;Befattning;Närstående;Korrigering;Beskrivning av korrigering;Är förstagångsrapportering;Är kopplad till aktieprogram;Karaktär;Instrumenttyp;Instrumentnamn;ISIN;Transaktionsdatum;Volym;Volymsenhet;Pris;Valuta;Handelsplats;Status\r\n" +
    //            "2025-06-04 13:37:17;Lagercrantz Group AB;5493002L6I4YHANEYR87;Marcus Käld;Marcus Käld;Annan medlem i bolagets administrations-, lednings- eller kontrollorgan;;;;Ja;Ja;Avyttring;Option;Optionsprogram 2021/25;;2025-06-04 00:00:00;2000,0;Antal;70,33;SEK;Utanför handelsplats;Aktuell;\r\n" +
    //            "2024-06-02 13:37:17;DemoCorp;0987654321ZYXWVU;Bertil Berg;Bertil Berg;CFO;Ja;Ja;;Ja;Ja;Sälj;Aktie;DemoAktie;SE0000000002;2024-06-01 00:00:00;200;st;234,56;SEK;XNGM;Publicerad\r\n";

    //    var handlerMock = new Mock<HttpMessageHandler>();
    //    handlerMock.Protected()
    //        .Setup<Task<HttpResponseMessage>>(
    //            "SendAsync",
    //            ItExpr.IsAny<HttpRequestMessage>(),
    //            ItExpr.IsAny<CancellationToken>()
    //        )
    //        .ReturnsAsync(new HttpResponseMessage
    //        {
    //            StatusCode = HttpStatusCode.OK,
    //            Content = new StringContent(csv, Encoding.Unicode), 
    //        });

    //    var httpClient = new HttpClient(handlerMock.Object);

    //    static CsvReader csvReaderFactory(TextReader reader)
    //    {
    //        var config = new CsvHelper.Configuration.CsvConfiguration(new CultureInfo("sv-SE"))
    //        {
    //            Delimiter = ";"
    //        };
    //        return new CsvReader(reader, config);
    //    }

    //    var service = new CsvFetchService(httpClient, csvReaderFactory);

    //    // Act
    //    var result = await service.FetchInsiderTradesAsync();

    //    // Assert
    //    Assert.NotNull(result);
    //    Assert.Equal(2, result.Count);

    //    Assert.Equal("Lagercrantz Group AB", result[0].Emittent);
    //    Assert.Equal("Marcus Käld", result[0].Anmälningsskyldig);
    //    Assert.Equal("Annan medlem i bolagets administrations-, lednings- eller kontrollorgan", result[0].Befattning);
    //    Assert.True(string.IsNullOrEmpty(result[0].ISIN));
    //    Assert.Equal(2000, result[0].Volym);
    //    Assert.Equal(70.33m, result[0].Pris);

    //    Assert.Equal("DemoCorp", result[1].Emittent);
    //    Assert.Equal("Bertil Berg", result[1].Anmälningsskyldig);
    //    Assert.Equal("CFO", result[1].Befattning);
    //    Assert.Equal("SE0000000002", result[1].ISIN);
    //    Assert.Equal(200, result[1].Volym);
    //    Assert.Equal(234.56m, result[1].Pris);
    //}

    [Fact]
    public async Task GetTransactions()
    {
        // Given 
        var csvFetchService = ServiceProviderFixture
                                   .GetRequiredService<CsvFetchService>(services => services.AuthorizedClient());

        // When
        var result = await csvFetchService.FetchInsiderTradesAsync();

        // Then
        await Verify(result);
    }
}
