using AktieKoll.Data;
using AktieKoll.Models;
using AktieKoll.Services;
using Microsoft.EntityFrameworkCore;
using static AktieKoll.Models.CsvDtoExtensions;

namespace AktieKoll.Tests.Unit;

public class TransactionsDbTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetInsiderTrades_ReturnsAddedTrades()
    {
        var ctx = CreateContext();
        var service = new InsiderTradeService(ctx);

        var trades = new List<InsiderTrade>
        {
            new()
            {
                CompanyName = "FooCorp",
                InsiderName = "Alice",
                Position = "CFO",
                TransactionType = "Buy",
                Shares = 100,
                Price = 10.5m,
                Currency = "SEK",
                Status = "Aktuell",
                PublishingDate = DateTime.Today,
                TransactionDate = DateTime.Today
            },
            new()
            {
                CompanyName = "BarCorp",
                InsiderName = "Bob",
                Position = "CEO",
                TransactionType = "Sell",
                Shares = 200,
                Price = 20.0m,
                Currency = "SEK",
                Status = "Aktuell",
                PublishingDate = DateTime.Today.AddDays(-1),
                TransactionDate = DateTime.Today.AddDays(-1)
            }
        };

        await service.AddInsiderTrades(trades);

        var result = await service.GetInsiderTrades();

        await Verify(result);
    }

    [Fact]
    public async Task GetInsiderTradesTop_ReturnsTopByValue()
    {
        var ctx = CreateContext();
        var service = new InsiderTradeService(ctx);

        var trades = Enumerable.Range(0, 3).Select(i => new InsiderTrade
        {
            CompanyName = $"Corp{i}",
            InsiderName = $"Name{i}",
            Position = "Exec",
            TransactionType = "Buy",
            Shares = 100 * (i + 1),
            Price = 10m * (i + 1),
            Currency = "SEK",
            Status = "Aktuell",
            PublishingDate = DateTime.Today,
            TransactionDate = DateTime.Today
        }).ToList();

        await service.AddInsiderTrades(trades);

        var result = await service.GetInsiderTradesTop();

        await Verify(result);
    }

    [Fact]
    public async Task AddInsiderTrades_Duplicate()
    {
        var ctx = CreateContext();
        var service = new InsiderTradeService(ctx);

        var trades = new List<InsiderTrade>
        {
            new()
            {
                CompanyName = "FooCorp",
                InsiderName = "Alice",
                Position = "CFO",
                TransactionType = "Buy",
                Shares = 100,
                Price = 10.5m,
                Currency = "SEK",
                Status = "Aktuell",
                PublishingDate = DateTime.Today,
                TransactionDate = DateTime.Today
            },
            new()
            {
                CompanyName = "FooCorp",
                InsiderName = "Alice",
                Position = "CFO",
                TransactionType = "Buy",
                Shares = 100,
                Price = 10.5m,
                Currency = "SEK",
                Status = "Aktuell",
                PublishingDate = DateTime.Today,
                TransactionDate = DateTime.Today
            },
            new()
            {
                CompanyName = "BarCorp",
                InsiderName = "Bob",
                Position = "CEO",
                TransactionType = "Sell",
                Shares = 200,
                Price = 20.0m,
                Currency = "SEK",
                Status = "Aktuell",
                PublishingDate = DateTime.Today.AddDays(-1),
                TransactionDate = DateTime.Today.AddDays(-1)
            }
        };

        await service.AddInsiderTrades(trades);

        var result = await service.GetInsiderTrades();

        await Verify(result);
    }

    [Theory]
    [InlineData("2025-06-23", "2025-06-24")]
    public async Task AddNewCsvData(DateTime fromDate, DateTime toDate)
    {
        var ctx = CreateContext();
        var csvFetchService = ServiceProviderFixture
                                   .GetRequiredService<CsvFetchService>(services => services.AuthorizedClient());

        var csvDto = await csvFetchService.FetchInsiderTradesAsync(fromDate, toDate);
        var trades = InsiderTradeMapper.MapDtosToTrades(csvDto);

        var service = new InsiderTradeService(ctx);
        await service.AddInsiderTrades(trades);

        var result = await service.GetInsiderTrades();

        await Verify(result);
    }

    [Fact]
    public async Task AddInsiderTrades_ExcludedTransactionsFiltered()
    {
        var ctx = CreateContext();
        var service = new InsiderTradeService(ctx);

        var csvDtos = new List<CsvDTO>
        {
            new()
            {
                Publiceringsdatum = DateTime.Today,
                Emittent = "FooCorp",
                LEI = string.Empty,
                Anmälningsskyldig = string.Empty,
                PersonNamn = "Alice",
                Befattning = "CFO",
                Karaktär = "Lån mottaget",
                Instrumenttyp = string.Empty,
                Instrumentnamn = string.Empty,
                Transaktionsdatum = DateTime.Today,
                Volym = 100,
                Volymsenhet = string.Empty,
                Pris = 10.5m,
                Valuta = "SEK",
                Handelsplats = string.Empty,
                Status = "Aktuell"
            },
            new()
            {
                Publiceringsdatum = DateTime.Today,
                Emittent = "FooCorp",
                LEI = string.Empty,
                Anmälningsskyldig = string.Empty,
                PersonNamn = "Alice",
                Befattning = "CFO",
                Karaktär = "Utdelning lämnad",
                Instrumenttyp = string.Empty,
                Instrumentnamn = string.Empty,
                Transaktionsdatum = DateTime.Today,
                Volym = 150,
                Volymsenhet = string.Empty,
                Pris = 15.5m,
                Valuta = "SEK",
                Handelsplats = string.Empty,
                Status = "Aktuell"
            },
            new()
            {
                Publiceringsdatum = DateTime.Today.AddDays(-1),
                Emittent = "Google",
                LEI = string.Empty,
                Anmälningsskyldig = string.Empty,
                PersonNamn = "Sundar",
                Befattning = "CEO",
                Karaktär = "Lösen ökning",
                Instrumenttyp = string.Empty,
                Instrumentnamn = string.Empty,
                Transaktionsdatum = DateTime.Today.AddDays(-1),
                Volym = 220,
                Volymsenhet = string.Empty,
                Pris = 300.0m,
                Valuta = "SEK",
                Handelsplats = string.Empty,
                Status = "Aktuell"
            },
            new()
            {
                Publiceringsdatum = DateTime.Today.AddDays(-1),
                Emittent = "Apple",
                LEI = string.Empty,
                Anmälningsskyldig = string.Empty,
                PersonNamn = "Tim Cook",
                Befattning = "CEO",
                Karaktär = "Lösen minskning",
                Instrumenttyp = string.Empty,
                Instrumentnamn = string.Empty,
                Transaktionsdatum = DateTime.Today.AddDays(-1),
                Volym = 2000,
                Volymsenhet = string.Empty,
                Pris = 10.0m,
                Valuta = "SEK",
                Handelsplats = string.Empty,
                Status = "Aktuell"
            },
            new()
            {
                Publiceringsdatum = DateTime.Today.AddDays(-1),
                Emittent = "BarCorp",
                LEI = string.Empty,
                Anmälningsskyldig = string.Empty,
                PersonNamn = "Bob",
                Befattning = "CEO",
                Karaktär = "Utdelning mottagen",
                Instrumenttyp = string.Empty,
                Instrumentnamn = string.Empty,
                Transaktionsdatum = DateTime.Today.AddDays(-1),
                Volym = 200,
                Volymsenhet = string.Empty,
                Pris = 20.0m,
                Valuta = "SEK",
                Handelsplats = string.Empty,
                Status = "Aktuell"
            },
            new()
            {
                Publiceringsdatum = DateTime.Today.AddDays(-1),
                Emittent = "BarCorp",
                LEI = string.Empty,
                Anmälningsskyldig = string.Empty,
                PersonNamn = "Bob",
                Befattning = "CEO",
                Karaktär = "Förvärv",
                Instrumenttyp = string.Empty,
                Instrumentnamn = string.Empty,
                Transaktionsdatum = DateTime.Today.AddDays(-1),
                Volym = 400,
                Volymsenhet = string.Empty,
                Pris = 40.0m,
                Valuta = "SEK",
                Handelsplats = string.Empty,
                Status = "Aktuell"
            }
        };

        var trades = InsiderTradeMapper.MapDtosToTrades(csvDtos);

        await service.AddInsiderTrades(trades);

        var result = await service.GetInsiderTrades();

        await Verify(result);
    }

    [Fact]
    public async Task GetInsiderTrades_FilterTransactionType()
    {
        var ctx = CreateContext();
        var service = new InsiderTradeService(ctx);

        var csvDtos = new List<CsvDTO>
        {
            new()
            {
                Publiceringsdatum = DateTime.Today,
                Emittent = "FooCorp",
                LEI = string.Empty,
                Anmälningsskyldig = string.Empty,
                PersonNamn = "Alice",
                Befattning = "CFO",
                Karaktär = "Interntransaktion – Förvärv",
                Instrumenttyp = string.Empty,
                Instrumentnamn = string.Empty,
                Transaktionsdatum = DateTime.Today,
                Volym = 100,
                Volymsenhet = string.Empty,
                Pris = 10.5m,
                Valuta = "SEK",
                Handelsplats = string.Empty,
                Status = "Aktuell"
            },
            new()
            {
                Publiceringsdatum = DateTime.Today,
                Emittent = "FooCorp",
                LEI = string.Empty,
                Anmälningsskyldig = string.Empty,
                PersonNamn = "Alice",
                Befattning = "CFO",
                Karaktär = "Interntransaktion – Avyttring",
                Instrumenttyp = string.Empty,
                Instrumentnamn = string.Empty,
                Transaktionsdatum = DateTime.Today,
                Volym = 150,
                Volymsenhet = string.Empty,
                Pris = 15.5m,
                Valuta = "SEK",
                Handelsplats = string.Empty,
                Status = "Aktuell"
            },
        };

        var trades = InsiderTradeMapper.MapDtosToTrades(csvDtos);

        await service.AddInsiderTrades(trades);

        var result = await service.GetInsiderTrades();

        await Verify(result);
    }

    [Theory]
    [InlineData("2025-06-23", "2025-06-24")]
    public async Task GetTopCompaniesByTransactions_ReturnsMostActive(DateTime fromDate, DateTime toDate)
    {
        var ctx = CreateContext();
        var csvFetchService = ServiceProviderFixture
                                   .GetRequiredService<CsvFetchService>(services => services.AuthorizedClient());

        var csvDto = await csvFetchService.FetchInsiderTradesAsync(fromDate, toDate);
        var trades = InsiderTradeMapper.MapDtosToTrades(csvDto);

        var service = new InsiderTradeService(ctx);
        await service.AddInsiderTrades(trades);

        var result = await service.GetTopCompaniesByTransactions();

        await Verify(result);
    }
}
