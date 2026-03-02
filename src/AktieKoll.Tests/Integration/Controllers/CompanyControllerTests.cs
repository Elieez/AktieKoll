using System.Net;
using AktieKoll.Data;
using AktieKoll.Dtos;
using AktieKoll.Models;
using AktieKoll.Tests.Fixture;
using AktieKoll.Tests.Integration.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AktieKoll.Tests.Integration.Controllers;

/// <summary>
/// Integration tests for CompanyController endpoints.
/// Tests use unique identifiers to avoid data pollution from parallel test execution.
/// </summary>
public class CompanyControllerTests(WebApplicationFactoryFixture factory) : IntegrationTestBase(factory)
{
    // Helper: Generate unique test identifier
    private static string TestId() => Guid.NewGuid().ToString("N")[..8].ToUpper();

    // Helper: Seed companies with consistent test data
    private async Task SeedCompaniesAsync(params Company[] companies)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Companies.AddRange(companies);
        await db.SaveChangesAsync(Token);
    }

    // Helper: Create test company with defaults
    private static Company MakeCompany(string code, string name, string? isin = null) => new()
    {
        Code = code,
        Name = name,
        Isin = isin,
        Currency = "SEK",
        Type = "Common Stock"
    };

    #region Search Tests

    [Fact]
    public async Task Search_WithValidQuery_ReturnsMatchingCompanies()
    {
        // Arrange
        var testId = TestId();
        await SeedCompaniesAsync(
            MakeCompany($"{testId}VOLVA", $"Volvo Group A {testId}", "SE0001"),
            MakeCompany($"{testId}VOLVB", $"Volvo Group B {testId}", "SE0002"),
            MakeCompany($"OTHER", "Other Company", "SE9999") // Should NOT match
        );

        // Act
        var response = await Client.GetTestAsync($"/api/company/search?q={testId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = await response.Content.ReadFromJsonTestAsync<List<CompanySearchResultDto>>();
        results.Should().HaveCount(2, "only companies with testId should match");
        results.Should().AllSatisfy(c => c.Code.Should().StartWith(testId));
    }

    [Fact]
    public async Task Search_PrioritizesStartsWithMatches()
    {
        // Arrange
        var testId = TestId();
        await SeedCompaniesAsync(
            MakeCompany($"{testId}START", $"{testId} Starts With"),      // Should be first
            MakeCompany($"MID{testId}DLE", $"Middle {testId} Contains"), // Should be second
            MakeCompany($"END{testId}", $"Ends With {testId}")           // Should be third
        );

        // Act
        var response = await Client.GetTestAsync($"/api/company/search?q={testId}");

        // Assert
        var results = await response.Content.ReadFromJsonTestAsync<List<CompanySearchResultDto>>();
        results.Should().HaveCount(3);

        // First result should start with testId (prioritized)
        results![0].Code.Should().StartWith(testId, "starts-with should be prioritized");
    }

    [Fact]
    public async Task Search_IsCaseInsensitive()
    {
        // Arrange
        var testId = TestId();
        await SeedCompaniesAsync(
            MakeCompany($"{testId}CASE", $"Test Company {testId}")
        );

        // Act
        var lowerResponse = await Client.GetTestAsync($"/api/company/search?q={testId.ToLower()}");
        var upperResponse = await Client.GetTestAsync($"/api/company/search?q={testId.ToUpper()}");

        // Assert
        var lowerResults = await lowerResponse.Content.ReadFromJsonTestAsync<List<CompanySearchResultDto>>();
        var upperResults = await upperResponse.Content.ReadFromJsonTestAsync<List<CompanySearchResultDto>>();

        lowerResults.Should().HaveCount(1);
        upperResults.Should().HaveCount(1);
        lowerResults![0].Code.Should().Be(upperResults![0].Code);
    }

    [Fact]
    public async Task Search_WithTooShortQuery_ReturnsBadRequest()
    {
        var response = await Client.GetTestAsync("/api/company/search?q=a");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_WithEmptyQuery_ReturnsBadRequest()
    {
        var response = await Client.GetTestAsync("/api/company/search?q=");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_WithMissingQuery_ReturnsBadRequest()
    {
        var response = await Client.GetTestAsync("/api/company/search");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_WithLimitTooLow_ReturnsBadRequest()
    {
        var response = await Client.GetTestAsync("/api/company/search?q=test&limit=0");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_WithLimitTooHigh_ReturnsBadRequest()
    {
        var response = await Client.GetTestAsync("/api/company/search?q=test&limit=100");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_RespectsCustomLimit()
    {
        // Arrange
        var testId = TestId();
        var companies = Enumerable.Range(1, 5)
            .Select(i => MakeCompany($"{testId}{i:D2}", $"Company {i} {testId}"))
            .ToArray();
        await SeedCompaniesAsync(companies);

        // Act
        var response = await Client.GetTestAsync($"/api/company/search?q={testId}&limit=3");

        // Assert
        var results = await response.Content.ReadFromJsonTestAsync<List<CompanySearchResultDto>>();
        results.Should().HaveCount(3, "limit parameter should be respected");
    }

    [Fact]
    public async Task Search_DefaultLimitIs10()
    {
        // Arrange
        var testId = TestId();
        var companies = Enumerable.Range(1, 15)
            .Select(i => MakeCompany($"{testId}{i:D2}", $"Company {i} {testId}"))
            .ToArray();
        await SeedCompaniesAsync(companies);

        // Act
        var response = await Client.GetTestAsync($"/api/company/search?q={testId}");

        // Assert
        var results = await response.Content.ReadFromJsonTestAsync<List<CompanySearchResultDto>>();
        results.Should().HaveCount(10, "default limit should be 10");
    }

    [Fact]
    public async Task Search_WithNoMatches_ReturnsEmptyArray()
    {
        // Arrange - use guaranteed non-existent identifier
        var nonExistent = $"NOEXIST{Guid.NewGuid():N}";

        // Act
        var response = await Client.GetTestAsync($"/api/company/search?q={nonExistent}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadFromJsonTestAsync<List<CompanySearchResultDto>>();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_ReturnsLightweightDto()
    {
        // Arrange
        var testId = TestId();
        await SeedCompaniesAsync(
            MakeCompany(testId, $"Test {testId}", "SE0000115420")
        );

        // Act
        var response = await Client.GetTestAsync($"/api/company/search?q={testId}");

        // Assert
        var results = await response.Content.ReadFromJsonTestAsync<List<CompanySearchResultDto>>();
        var dto = results!.Single();

        // Verify structure
        dto.Code.Should().Be(testId);
        dto.Name.Should().Contain(testId);
        dto.Isin.Should().Be("SE0000115420");

        // Verify it's NOT the full DTO (no extra fields)
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().NotContain("currency");
        json.Should().NotContain("type");
        json.Should().NotContain("lastUpdated");
    }

    #endregion

    #region GetByCode Tests

    [Fact]
    public async Task GetByCode_WithValidCode_ReturnsFullDto()
    {
        // Arrange
        var testId = TestId();
        await SeedCompaniesAsync(
            MakeCompany(testId, $"Test Company {testId}", "SE0000115420")
        );

        // Act
        var response = await Client.GetTestAsync($"/api/company/{testId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var company = await response.Content.ReadFromJsonTestAsync<CompanyDto>();
        company.Should().NotBeNull();
        company!.Code.Should().Be(testId);
        company.Name.Should().Contain(testId);
        company.Isin.Should().Be("SE0000115420");
        company.Currency.Should().Be("SEK");
        company.Type.Should().Be("Common Stock");
        company.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetByCode_WithNonExistentCode_ReturnsNotFound()
    {
        // Arrange
        var nonExistent = $"NONE{Guid.NewGuid():N}"[..10];

        // Act
        var response = await Client.GetTestAsync($"/api/company/{nonExistent}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByCode_IsCaseInsensitive()
    {
        // Arrange
        var testId = TestId();
        await SeedCompaniesAsync(
            MakeCompany(testId, $"Test {testId}")
        );

        // Act
        var lowerResponse = await Client.GetTestAsync($"/api/company/{testId.ToLower()}");
        var upperResponse = await Client.GetTestAsync($"/api/company/{testId.ToUpper()}");

        // Assert
        lowerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        upperResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var lowerCompany = await lowerResponse.Content.ReadFromJsonTestAsync<CompanyDto>();
        var upperCompany = await upperResponse.Content.ReadFromJsonTestAsync<CompanyDto>();

        lowerCompany!.Code.Should().Be(upperCompany!.Code);
    }

    [Fact]
    public async Task GetByCode_ReturnsJsonContentType()
    {
        // Arrange
        var testId = TestId();
        await SeedCompaniesAsync(MakeCompany(testId, $"Test {testId}"));

        // Act
        var response = await Client.GetTestAsync($"/api/company/{testId}");

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    #endregion

    #region GetAll Tests

    [Fact]
    public async Task GetAll_IncludesSeededCompanies()
    {
        // Arrange
        var testId = TestId();
        await SeedCompaniesAsync(
            MakeCompany($"{testId}A", $"Alpha {testId}"),
            MakeCompany($"{testId}B", $"Beta {testId}"),
            MakeCompany($"{testId}C", $"Gamma {testId}")
        );

        // Act
        var response = await Client.GetTestAsync("/api/company");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = await response.Content.ReadFromJsonTestAsync<List<CompanyDto>>();

        // Verify OUR companies are present (may have others from parallel tests)
        results.Should().Contain(c => c.Code == $"{testId}A");
        results.Should().Contain(c => c.Code == $"{testId}B");
        results.Should().Contain(c => c.Code == $"{testId}C");
    }

    [Fact]
    public async Task Search_ReturnsResultsSortedByRelevance()
    {
        // Arrange - Tests that search returns sorted results (what users care about)
        var testId = TestId();
        await SeedCompaniesAsync(
            MakeCompany($"{testId}Z", $"Zebra {testId}"),
            MakeCompany($"{testId}A", $"Alpha {testId}"),
            MakeCompany($"{testId}M", $"Middle {testId}")
        );

        // Act - User searches
        var response = await Client.GetTestAsync($"/api/company/search?q={testId}&limit=50");

        // Assert - Results are sorted by name
        var results = await response.Content.ReadFromJsonTestAsync<List<CompanySearchResultDto>>();
        results.Should().HaveCount(3);

        var names = results!.Select(c => c.Name).ToList();
        names.Should().BeInAscendingOrder("search results should be sorted by name");
    }

    [Fact]
    public async Task GetByCode_ReturnsCompleteCompanyInformation()
    {
        // Arrange - Tests that we get full DTO (what users need)
        var testId = TestId();
        await SeedCompaniesAsync(
            MakeCompany(testId, $"Full Info Test {testId}", "SE0000115420")
        );

        // Act - User gets company details
        var response = await Client.GetTestAsync($"/api/company/{testId}");

        // Assert - All fields present
        var dto = await response.Content.ReadFromJsonTestAsync<CompanyDto>();

        dto.Should().NotBeNull();
        dto!.Code.Should().Be(testId);
        dto.Name.Should().Contain(testId);
        dto.Isin.Should().Be("SE0000115420");
        dto.Currency.Should().Be("SEK");
        dto.Type.Should().Be("Common Stock");
        dto.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    #endregion
}