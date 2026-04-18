using AktieKoll.Models;
using AktieKoll.Services;
using AktieKoll.Tests.Shared.TestHelpers;

namespace AktieKoll.Tests.Unit;

public class CompanyServiceTests
{
    [Fact]
    public async Task SearchCompanies_FindsByCode()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        ctx.Companies.Add(new Models.Company
        {
            Code = "VOLV-B",
            Name = "Volvo B",
            Isin = "SE0000115420",
            Currency = "SEK",
            Type = "Common Stock"
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new CompanyService(ctx);

        var results = await service.SearchCompaniesAsync("VOLV");

        Assert.Single(results);
        Assert.Equal("VOLV-B", results.First().Code);
    }

    [Fact]
    public async Task SearchCompanies_FindsByName()
    {
        // Arrange
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

        var service = new CompanyService(ctx);

        // Act
        var results = await service.SearchCompaniesAsync("volvo");

        // Assert
        Assert.Single(results);
    }

    [Fact]
    public async Task SearchCompanies_PrioritizesStartsWith()
    {
        // Arrange
        var ctx = ServiceTestHelpers.CreateContext();
        ctx.Companies.AddRange(
            new Company 
            { 
                Code = "INV-B", 
                Name = "Investor AB", 
                Isin = null, 
                Currency = "SEK", 
                Type = "Common Stock" 
            },
            new Company { 
                Code = "SWEINV", 
                Name = "Sweinv AB", 
                Isin = null, 
                Currency = "SEK", 
                Type = "Common Stock" 
            }
        );
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new CompanyService(ctx);

        // Act
        var results = (await service.SearchCompaniesAsync("inv")).ToList();

        // Assert
        Assert.Equal(2, results.Count);
        // First should start with "inv"
        Assert.True(
            results[0].Code.StartsWith("INV", StringComparison.OrdinalIgnoreCase) ||
            results[0].Name.StartsWith("Inv", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public async Task GetCompanyByCode_ReturnsCompany()
    {
        // Arrange
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

        var service = new CompanyService(ctx);

        // Act
        var result = await service.GetCompanyByCodeAsync("VOLV-B");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("VOLV-B", result.Code);
    }

    [Fact]
    public async Task GetCompanyByCode_NotFound_ReturnsNull()
    {
        // Arrange
        var ctx = ServiceTestHelpers.CreateContext();
        var service = new CompanyService(ctx);

        // Act
        var result = await service.GetCompanyByCodeAsync("NONEXISTENT");

        // Assert
        Assert.Null(result);
    }
}
