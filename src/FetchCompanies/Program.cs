using System.Net.Http.Json;
using AktieKoll.Data;
using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateApplicationBuilder(args).Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("FetchCompanies");

var apiToken = Environment.GetEnvironmentVariable("EOD_API_TOKEN");
if (string.IsNullOrWhiteSpace(apiToken))
{
    logger.LogCritical("EOD_API_TOKEN environment variable not set");
    return 1;
}

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostgresConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    logger.LogCritical("ConnectionStrings__PostgresConnection environment variable not set");
    return 1;
}

using var httpClient = new HttpClient();
var url = $"https://eodhd.com/api/exchange-symbol-list/ST?api_token={apiToken}&fmt=json";

logger.LogInformation("Fetching Swedish companies from EOD Historical Data...");

HttpResponseMessage response;
try
{
    response = await httpClient.GetAsync(url);
    response.EnsureSuccessStatusCode();
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to fetch companies from EOD API");
    return 1;
}

var eodCompanies = await response.Content.ReadFromJsonAsync<List<EodCompany>>();

if (eodCompanies == null || eodCompanies.Count == 0)
{
    logger.LogWarning("No companies returned from EOD API");
    return 0;
}

logger.LogInformation("Total companies fetched: {Count}", eodCompanies.Count);

var incomingCompanies = eodCompanies
    .Where(c => c.Type == "Common Stock")
    .ToList();

logger.LogInformation("Filtered to {Count} common stocks", incomingCompanies.Count);

var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
optionsBuilder.UseNpgsql(connectionString);

await using var context = new ApplicationDbContext(optionsBuilder.Options);

var incomingCode = incomingCompanies.Select(c => c.Code).ToHashSet();
var existingByCode = await context.Companies
    .Where(c => incomingCode.Contains(c.Code))
    .ToDictionaryAsync(c => c.Code);

int added = 0;
int updated = 0;

foreach (var eod in incomingCompanies)
{
    if (existingByCode.TryGetValue(eod.Code, out var existing))
    {
        existing.Name = eod.Name;
        existing.Isin = eod.Isin ?? existing.Isin;
        existing.Currency = eod.Currency;
        existing.Type = eod.Type;
        existing.LastUpdated = DateTime.UtcNow;
        updated++;
    }
    else
    {
        context.Companies.Add(new Company
        {
            Code = eod.Code,
            Name = eod.Name,
            Isin = eod.Isin,
            Currency = eod.Currency,
            Type = eod.Type
        });
        added++;
    }
}

await context.SaveChangesAsync();

logger.LogInformation("{Added} companies added, {Updated} updated.", added, updated);
return 0;

record EodCompany(
    string Code,
    string Name,
    string? Isin,
    string Currency,
    string Type,
    string Country,
    string Exchange
);