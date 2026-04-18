using AktieKoll.Data;
using AktieKoll.Models;
using AktieKoll.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace AktieKoll.Tests.Shared.TestHelpers;

public static class ServiceTestHelpers
{
    public static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    public static SymbolService CreateSymbolService(ApplicationDbContext ctx, IMemoryCache? cache = null)
    {
        return new SymbolService(ctx, cache ?? new MemoryCache(new MemoryCacheOptions()), NullLogger<SymbolService>.Instance);
    }

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static InsiderTradeService CreateInsiderTradeService(ApplicationDbContext ctx, TimeProvider? timeProvider = null)
    {
        var symbolService = CreateSymbolService(ctx);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var tradeLogger = NullLogger<InsiderTradeService>.Instance;

        
        return new InsiderTradeService(ctx, symbolService, cache, timeProvider ?? TimeProvider.System, tradeLogger);
    }

    public static async Task SeedCompanies(
        ApplicationDbContext ctx,
        params (string Isin, string Code)[] companies)
    {
        var converted = companies
            .Select(c => (Isin: (string?)c.Isin, c.Code, Name: (string?)null))
            .ToArray();

        await SeedCompanies(ctx, converted);
    }

    public static async Task SeedCompanies(ApplicationDbContext ctx, params (string? Isin, string Code, string? Name)[] companies)
    {
        foreach (var (isin, code, name) in companies)
        {
            ctx.Companies.Add(new Company
            {
                Code = code,
                Name = name ?? $"Company {code}",
                Isin = isin,
                Currency = "SEK",
                Type = "Common Stock"
            });
        }
        await ctx.SaveChangesAsync();
    }
}
