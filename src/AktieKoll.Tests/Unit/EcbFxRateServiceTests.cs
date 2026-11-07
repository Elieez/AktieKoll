using System.Net;
using System.Text;
using AktieKoll.Models;
using AktieKoll.Services;
using AktieKoll.Tests.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static AktieKoll.Extensions.CsvDtoExtensions;

namespace AktieKoll.Tests.Unit;

public class EcbFxRateServiceTests
{
    private const string EcbXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <gesmes:Envelope xmlns:gesmes="http://www.gesmes.org/xml/2002-08-01" xmlns="http://www.ecb.int/vocabulary/2002-08-01/eurofxref">
          <Cube>
            <Cube time="2026-11-06">
              <Cube currency="USD" rate="1.10"/>
              <Cube currency="SEK" rate="11.00"/>
              <Cube currency="NOK" rate="10.00"/>
            </Cube>
          </Cube>
        </gesmes:Envelope>
        """;

    private static EcbFxRateService CreateService(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var client = new HttpClient(new StubHandler(body, status));
        return new EcbFxRateService(client, NullLogger<EcbFxRateService>.Instance);
    }

    private static InsiderTrade MakeTrade(string currency, DateTime transactionDate, decimal price = 10m, long shares = 100)
        => FakeDTO.MakeCsvDto(d =>
        {
            d.Valuta = currency;
            d.Pris = price;
            d.Volym = shares;
            d.Transaktionsdatum = transactionDate;
        }).ToInsiderTrade();

    [Fact]
    public async Task ApplyValueSek_ConvertsViaEurCrossRate()
    {
        var service = CreateService(EcbXml);
        var trade = MakeTrade("USD", new DateTime(2026, 11, 6));

        await service.ApplyValueSekAsync([trade]);

        // 100 × 10 USD × (11.00 / 1.10) = 10 000 SEK
        trade.ValueSek.Should().Be(10_000m);
    }

    [Fact]
    public async Task ApplyValueSek_UsesPreviousBankingDayOnWeekend()
    {
        var service = CreateService(EcbXml);
        var trade = MakeTrade("NOK", new DateTime(2026, 11, 8)); // Sunday

        await service.ApplyValueSekAsync([trade]);

        trade.ValueSek.Should().Be(1_100m);
    }

    [Fact]
    public async Task ApplyValueSek_LeavesSekTradesUntouched()
    {
        var service = CreateService(EcbXml);
        var trade = MakeTrade("SEK", new DateTime(2026, 11, 6));

        await service.ApplyValueSekAsync([trade]);

        trade.ValueSek.Should().Be(1_000m); // set by the mapper, no FX needed
    }

    [Fact]
    public async Task ApplyValueSek_UnknownCurrencyOrNetworkFailure_LeavesNull()
    {
        var unknown = MakeTrade("XYZ", new DateTime(2026, 11, 6));
        await CreateService(EcbXml).ApplyValueSekAsync([unknown]);
        unknown.ValueSek.Should().BeNull();

        var offline = MakeTrade("USD", new DateTime(2026, 11, 6));
        await CreateService("", HttpStatusCode.InternalServerError).ApplyValueSekAsync([offline]);
        offline.ValueSek.Should().BeNull();
    }

    [Fact]
    public void ToInsiderTrade_MapsFiFlagsAndDetails()
    {
        var trade = FakeDTO.MakeCsvDto(d =>
        {
            d.Närstående = "Ja";
            d.Aktieprogram = "Ja";
            d.LEI = "5493001KJTIIGC8Y1R12";
            d.Instrumenttyp = "Aktie";
            d.Handelsplats = "NASDAQ STOCKHOLM AB";
        }).ToInsiderTrade();

        trade.IsCloselyAssociated.Should().BeTrue();
        trade.IsShareProgram.Should().BeTrue();
        trade.Lei.Should().Be("5493001KJTIIGC8Y1R12");
        trade.InstrumentType.Should().Be("Aktie");
        trade.TradingVenue.Should().Be("NASDAQ STOCKHOLM AB");

        var plain = FakeDTO.MakeCsvDto().ToInsiderTrade();
        plain.IsCloselyAssociated.Should().BeFalse();
        plain.IsShareProgram.Should().BeFalse();
        plain.Lei.Should().BeNull();
    }

    [Fact]
    public void ToInsiderTrade_TotalEnteredAsPrice_ValueSekLeftOut()
    {
        // Real filing: 634 146 shares with the total amount (1 585 365 kr) entered as price.
        var trade = FakeDTO.MakeCsvDto(d =>
        {
            d.Volym = 634_146;
            d.Pris = 1_585_365m;
        }).ToInsiderTrade();

        trade.ValueSek.Should().BeNull();
    }

    [Fact]
    public async Task ApplyValueSek_ImplausibleForeignValue_LeftNull()
    {
        var trade = MakeTrade("USD", new DateTime(2026, 11, 6), price: 10_000_000m, shares: 1_000_000);

        await CreateService(EcbXml).ApplyValueSekAsync([trade]);

        trade.ValueSek.Should().BeNull();
    }

    private sealed class StubHandler(string body, HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/xml")
            });
    }
}
