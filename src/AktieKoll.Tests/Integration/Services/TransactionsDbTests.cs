using AktieKoll.Models;
using AktieKoll.Services;
using AktieKoll.Tests.Extensions;
using AktieKoll.Tests.Shared.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static AktieKoll.Extensions.CsvDtoExtensions;

namespace AktieKoll.Tests.Integration.Services;

public class TransactionsDbTests
{

    [Fact]
    public async Task GetInsiderTrades_ReturnsAddedTrades()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);

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

        var result = await service.GetInsiderTradesPage(1, 100);

        await Verify(result);
    }

    [Fact]
    public async Task GetInsiderTradesTop_ReturnsTopByValue()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);

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
        var ctx = ServiceTestHelpers.CreateContext();

        await ServiceTestHelpers.SeedCompanies(ctx,
        ("SE001", "FOO"),
        ("SE002", "BAR"));

        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);

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
                Isin = "SE001",
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
                Isin = "SE001",
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
                Isin = "SE002",
                PublishingDate = DateTime.Today.AddDays(-1),
                TransactionDate = DateTime.Today.AddDays(-1)
            }
        };

        await service.AddInsiderTrades(trades);

        var result = await service.GetInsiderTradesPage(1, 100);

        await Verify(result);
    }

    [Theory]
    [InlineData("2025-06-23", "2025-06-24")]
    public async Task AddNewCsvData(DateTime fromDate, DateTime toDate)
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var csvFetchService = ServiceProviderFixture
                                   .GetRequiredService<CsvFetchService>(services => services.AuthorizedClient());

        var csvDto = await csvFetchService.FetchInsiderTradesAsync(fromDate, toDate);
        var trades = InsiderTradeMapper.MapDtosToTrades(csvDto);

        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);
        await service.AddInsiderTrades(trades);

        var result = await service.GetInsiderTradesPage(1, 100);

        await Verify(result);
    }


    [Theory]
    [InlineData("Lån mottaget")]
    [InlineData("Utdelning lämnad")]
    [InlineData("Lösen minskning")]
    [InlineData("Lösen ökning")]
    [InlineData("Lån återgång ökning")]
    [InlineData("Lån återgång minskning")]
    [InlineData("Utbyte minskning")]
    [InlineData("Utbyte ökning")]
    [InlineData("Pantsättning")]
    [InlineData("Bodelning minskning")]
    [InlineData("Bodelning ökning")]
    [InlineData("Arv mottagen")]
    [InlineData("Konvertering ökning")]
    [InlineData("Lån utlåning")]
    public async Task AddInsiderTrades_ExcludedTransactionsFiltered(string transactionType)
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);

        var csvDtos = new List<CsvDTO>
        {
            FakeDTO.MakeCsvDto(d => { d.Karaktär = transactionType; }),

            FakeDTO.MakeCsvDto(d => { d.Karaktär = "Förvärv"; })
        };

        var trades = InsiderTradeMapper.MapDtosToTrades(csvDtos);

        await service.AddInsiderTrades(trades);

        var result = await service.GetInsiderTradesPage(1, 100);

        await Verify(result);
    }

    [Theory]
    [InlineData("Swap")]
    [InlineData("Ränteswap")]
    [InlineData("BTU")]
    [InlineData("Teckningsrätt/Uniträtt")]
    [InlineData("Kreditderivat")]
    [InlineData("Terminskontrakt")]
    [InlineData("Option")]
    public async Task AddInsiderTrades_ExcludedInstrumentTypeFiltered(string instrumentType)
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);

        var csvDtos = new List<CsvDTO>
        {
            FakeDTO.MakeCsvDto(d => { d.Instrumenttyp = instrumentType; }),

            FakeDTO.MakeCsvDto(d => { d.Instrumenttyp = "Aktie"; })
        };

        var trades = InsiderTradeMapper.MapDtosToTrades(csvDtos);

        await service.AddInsiderTrades(trades);

        var result = await service.GetInsiderTradesPage(1, 100);

        await Verify(result);
    }

    [Fact]
    public async Task AddInsiderTrades_FilterPositionName()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var service = ServiceTestHelpers.   CreateInsiderTradeService(ctx);

        var csvDtos = new List<CsvDTO>
        {
            FakeDTO.MakeCsvDto(d => { d.Befattning = "Verkställande direktör (VD)"; }),
            FakeDTO.MakeCsvDto(d => { d.Befattning = "Ekonomichef/finanschef/finansdirektör"; }),
            FakeDTO.MakeCsvDto(d => { d.Befattning = "Annan medlem i bolagets administrations-, lednings- eller kontrollorgan"; }),
            FakeDTO.MakeCsvDto(d => { d.Befattning = "Arbetstagarrepresentant i styrelsen eller arbetstagarsuppleant"; }),
            FakeDTO.MakeCsvDto(d => { d.Befattning = "Styrelseledamot"; })
        };

        var trades = InsiderTradeMapper.MapDtosToTrades(csvDtos);

        await service.AddInsiderTrades(trades);

        var result = await service.GetInsiderTradesPage(1, 100);

        await Verify(result);
    }

    [Fact]
    public async Task GetInsiderTrades_FilterTransactionType()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);

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
                ISIN = "SE0001",
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
                ISIN = "SE0001",
                Status = "Aktuell"
            },
        };

        var trades = InsiderTradeMapper.MapDtosToTrades(csvDtos);

        await service.AddInsiderTrades(trades);

        var result = await service.GetInsiderTradesPage(1, 100);

        await Verify(result);
    }

    [Theory]
    [InlineData(null, 3, 365)]
    [InlineData("eEducation Albert", null, 365)] //patch
    [InlineData(null, null, 365)]
    public async Task GetTransactionCountBuy_ReturnsMostActive(string? companyName, int? top, int days)
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var csvFetchService = ServiceProviderFixture
                                   .GetRequiredService<CsvFetchService>(services => services.AuthorizedClient());

        var fromDate = new DateTime(2025, 6, 23);
        var toDate = new DateTime(2025, 6, 24);

        var csvDto = await csvFetchService.FetchInsiderTradesAsync(fromDate, toDate);
        var trades = InsiderTradeMapper.MapDtosToTrades(csvDto);

        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);
        await service.AddInsiderTrades(trades);

        var result = await service.GetTransactionCountBuy(companyName, days, top);

        await Verify(result);
    }

    [Theory]
    [InlineData(null, 2, 365)]
    [InlineData("Zinzino AB", null, 365)] //patch
    [InlineData(null, null, 365)]
    public async Task GetTransactionCountSell_ReturnsMostActive(string? companyName, int? top, int days)
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var csvFetchService = ServiceProviderFixture
                                   .GetRequiredService<CsvFetchService>(services => services.AuthorizedClient());

        var fromDate = new DateTime(2025, 6, 23);
        var toDate = new DateTime(2025, 6, 24);

        var csvDto = await csvFetchService.FetchInsiderTradesAsync(fromDate, toDate);
        var trades = InsiderTradeMapper.MapDtosToTrades(csvDto);

        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);
        await service.AddInsiderTrades(trades);

        var result = await service.GetTransactionCountSell(companyName, days, top);

        await Verify(result);
    }

    [Theory]
    [InlineData("2025-06-23", "2025-06-24", "eEducation Albert")]
    [InlineData("2025-06-23", "2025-06-24", "eEducation Albert ab")]
    [InlineData("2025-06-23", "2025-06-24", "eEducation Albert ab (publ)")]

    public async Task GetTradesByCompany_ReturnTrades(DateTime fromDate, DateTime toDate, string companyName)
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var csvFetchService = ServiceProviderFixture
                                   .GetRequiredService<CsvFetchService>(services => services.AuthorizedClient());

        var csvDto = await csvFetchService.FetchInsiderTradesAsync(fromDate, toDate);
        var trades = InsiderTradeMapper.MapDtosToTrades(csvDto);

        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);
        await service.AddInsiderTrades(trades);

        var result = await service.GetInsiderTradesByCompany(companyName);

        await Verify(result);
    }

    [Fact]
    public async Task ResolveSymbols_FallsBackToCompanyName_WhenIsinNotFound()
    {
        var ctx = ServiceTestHelpers.CreateContext();

        ctx.Companies.Add(new Company
        {
            Code = "VOLV-B",
            Name = "Volvo Group",
            Isin = null,
            Currency = "SEK",
            Type = "Common Stock"
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var logger = NullLogger<SymbolService>.Instance;
        var cache = new MemoryCache(new MemoryCacheOptions());
        var symbolService = new SymbolService(ctx, cache, logger);

        var trades = new List<InsiderTrade>
        {
            new()
            {
                CompanyName = "Volvo Group",
                InsiderName = "Alice",
                Position = "CFO",
                TransactionType = "Buy",
                Shares = 100,
                Price = 10.5m,
                Currency = "SEK",
                Isin = "SE0000115420", // Wrong ISIN (VOLVO A)
                Status = "Aktuell",
                PublishingDate = DateTime.Today,
                TransactionDate = DateTime.Today
            }
        };

        await symbolService.ResolveSymbolsAsync(trades);

        Assert.Equal("VOLV-B", trades[0].Symbol);
    }

    [Fact]
    public async Task ResolveSymbols_MatchesWithAndWithoutAB()
    {
        var ctx = ServiceTestHelpers.CreateContext();

        ctx.Companies.Add(new Company
        {
            Code = "VOLV-B",
            Name = "Volvo Group AB",
            Isin = null,
            Currency = "SEK",
            Type = "Common Stock"
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var logger = NullLogger<SymbolService>.Instance;
        var cache = new MemoryCache(new MemoryCacheOptions());

        var symbolService = new SymbolService(ctx, cache, logger);

        var trades = new List<InsiderTrade>
        {
            new()
            {
                CompanyName = "Volvo Group", // ← WITHOUT AB
                Isin = "SE0000115420", // Wrong ISIN to force name fallback
                InsiderName = "Test",
                Position = "CEO",
                TransactionType = "Förvärv",
                Shares = 100,
                Price = 10m,
                Currency = "SEK",
                Status = "Aktuell",
                PublishingDate = DateTime.Today,
                TransactionDate = DateTime.Today
            }
        };

        // Act
        await symbolService.ResolveSymbolsAsync(trades);

        // Assert
        Assert.Equal("VOLV-B", trades[0].Symbol);
    }

    [Fact]
    public async Task ResolveSymbols_MatchesWithPubl()
    {
        // Arrange
        var ctx = ServiceTestHelpers.CreateContext();

        ctx.Companies.Add(new Company
        {
            Code = "ERIC-B",
            Name = "Ericsson AB (publ)",
            Isin = null,
            Currency = "SEK",
            Type = "Common Stock"
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var logger = NullLogger<SymbolService>.Instance;
        var cache = new MemoryCache(new MemoryCacheOptions());
        var symbolService = new SymbolService(ctx, cache,logger);

        var trades = new List<InsiderTrade>
    {
        new()
        {
            CompanyName = "Ericsson", // ← Without AB or (publ)
            Isin = "SE0000108656",
            InsiderName = "Test",
            Position = "CEO",
            TransactionType = "Förvärv",
            Shares = 100,
            Price = 10m,
            Currency = "SEK",
            Status = "Aktuell",
            PublishingDate = DateTime.Today,
            TransactionDate = DateTime.Today
        }
    };

        // Act
        await symbolService.ResolveSymbolsAsync(trades);

        // Assert
        Assert.Equal("ERIC-B", trades[0].Symbol);
    }

    [Fact]
    public async Task ResolveSymbols_MultipleMatches_PicksBShare()
    {
        // Arrange
        var ctx = ServiceTestHelpers.CreateContext();

        // Both A and B shares for same company
        ctx.Companies.AddRange(
            new Company { Code = "VOLV-A", Name = "Volvo Group AB", Isin = "SE0000115422", Currency = "SEK", Type = "Common Stock" },
            new Company { Code = "VOLV-B", Name = "Volvo Group AB", Isin = "SE0000115420", Currency = "SEK", Type = "Common Stock" }
        );
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var logger = NullLogger<SymbolService>.Instance;
        var cache = new MemoryCache(new MemoryCacheOptions());
        var symbolService = new SymbolService(ctx, cache, logger);

        var trades = new List<InsiderTrade>
    {
        new()
        {
            CompanyName = "Volvo Group", // Ambiguous - could be A or B
            Isin = "SE9999999999", // Wrong ISIN to force name fallback
            InsiderName = "Test",
            Position = "CEO",
            TransactionType = "Förvärv",
            Shares = 100,
            Price = 10m,
            Currency = "SEK",
            Status = "Aktuell",
            PublishingDate = DateTime.Today,
            TransactionDate = DateTime.Today
        }
    };

        // Act
        await symbolService.ResolveSymbolsAsync(trades);

        // Assert - Should pick B-share (most liquid in Sweden)
        Assert.Equal("VOLV-B", trades[0].Symbol);
    }

    [Fact]
    public async Task ResolveSymbols_NoSuffix_PicksNoSuffixOverAB()
    {
        // Arrange
        var ctx = ServiceTestHelpers.CreateContext();

        // Mix: A-share, B-share, and no suffix
        ctx.Companies.AddRange(
            new Company { Code = "TEST-A", Name = "Test Corp AB", Isin = null, Currency = "SEK", Type = "Common Stock" },
            new Company { Code = "TEST-B", Name = "Test Corp AB", Isin = null, Currency = "SEK", Type = "Common Stock" },
            new Company { Code = "TEST", Name = "Test Corp AB", Isin = null, Currency = "SEK", Type = "Common Stock" }
        );
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var logger = NullLogger<SymbolService>.Instance;
        var cache = new MemoryCache(new MemoryCacheOptions());
        var symbolService = new SymbolService(ctx, cache, logger);

        var trades = new List<InsiderTrade>
    {
        new()
        {
            CompanyName = "Test Corp",
            Isin = "SE9999999999",
            InsiderName = "Test",
            Position = "CEO",
            TransactionType = "Förvärv",
            Shares = 100,
            Price = 10m,
            Currency = "SEK",
            Status = "Aktuell",
            PublishingDate = DateTime.Today,
            TransactionDate = DateTime.Today
        }
    };

        // Act
        await symbolService.ResolveSymbolsAsync(trades);

        // Assert - Should pick B-share first (highest priority for Swedish stocks)
        Assert.Equal("TEST-B", trades[0].Symbol);
    }

    [Fact]
    public async Task GetTransactionStats_ReturnsModellSuccess()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var csvFetchService = ServiceProviderFixture
                                   .GetRequiredService<CsvFetchService>(services => services.AuthorizedClient());

        var fromDate = new DateTime(2025, 6, 23);
        var toDate = new DateTime(2025, 6, 24);

        var fakeTime = new FakeTimeProvider();
        fakeTime.SetUtcNow(new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero));

        var csvDto = await csvFetchService.FetchInsiderTradesAsync(fromDate, toDate);
        var trades = InsiderTradeMapper.MapDtosToTrades(csvDto);

        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx,fakeTime);
        await service.AddInsiderTrades(trades);

        var result = await service.GetYtdTransactionStatsAsync();

        await Verify(result);
    }
}