
using System.Net.Http.Json;
using AktieKoll.Data;
using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;

var apiToken = Environment.GetEnvironmentVariable("EOD_API_TOKEN")
    ?? throw new Exception("EOD_API_TOKEN environment variable not set");

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
    ?? throw new Exception("ConnectionStrings__PostgresConnection not set");

var httpClient = new HttpClient();
var url = $"https://eodhd.com/api/exchange-symbol-list/ST?api_token={apiToken}&fmt=json";

Console.WriteLine("Fetching Swedish companies from EOD Historical Data...");

var response = await httpClient.GetAsync(url);
response.EnsureSuccessStatusCode();

var eodCompanies = await response.Content.ReadFromJsonAsync<List<EodCompany>>();

if (eodCompanies == null || eodCompanies.Count == 0)
{
    Console.WriteLine("No companies returned from API");
    return;
}

Console.WriteLine($"Total companies fetched: {eodCompanies.Count}");

var companies = eodCompanies
    .Where(c => c.Type == "Common Stock")
    .Select(c => new Company
    {
        Code = c.Code,
        Name = c.Name,
        Isin = c.Isin,
        Currency = c.Currency,
        Type = c.Type
    })
    .ToList();

Console.WriteLine($"Filtered to {companies.Count} common stocks");

var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
optionsBuilder.UseNpgsql(connectionString);

await using var context = new ApplicationDbContext(optionsBuilder.Options);

int added = 0;
int updated = 0;

foreach (var company in companies)
{
    Company? existing;

    if (!string.IsNullOrEmpty(company.Isin))
    {
        existing = await context.Companies
            .FirstOrDefaultAsync(c => c.Isin == company.Isin);
    }
    else
    {
        existing = await context.Companies
            .FirstOrDefaultAsync(c => c.Code == company.Code);
    }

    if (existing == null)
    {
        context.Companies.Add(company);
        added++;
    }
    else
    {
        existing.Code = company.Code;
        existing.Name = company.Name;
        existing.Isin = company.Isin;
        existing.Currency = company.Currency;
        existing.Type = company.Type;
        existing.LastUpdated = DateTime.UtcNow;
        updated++;
    }
}

await context.SaveChangesAsync();

Console.WriteLine($"✅ {added} companies added, {updated} updated.");

record EodCompany(
    string Code,
    string Name,
    string? Isin,
    string Currency,
    string Type,
    string Country,
    string Exchange
);