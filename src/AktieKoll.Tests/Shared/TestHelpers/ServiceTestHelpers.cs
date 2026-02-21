using AktieKoll.Data;
using AktieKoll.Models;
using AktieKoll.Services;
using Microsoft.EntityFrameworkCore;
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

    public static InsiderTradeService CreateInsiderTradeService(ApplicationDbContext ctx)
    {
        var logger = NullLogger<SymbolService>.Instance;
        var symbolService = new SymbolService(ctx, logger);
        return new InsiderTradeService(ctx, symbolService);
    }

    public static async Task SeedCompanies(ApplicationDbContext ctx, params (string Isin, string Code)[] companies)
    {
        foreach (var (isin, code) in companies)
        {
            ctx.Companies.Add(new Company
            {
                Code = code,
                Name = $"Company {code}",
                Isin = isin,
                Currency = "SEK",
                Type = "Common Stock"
            });
        }
        await ctx.SaveChangesAsync();
    }
}
