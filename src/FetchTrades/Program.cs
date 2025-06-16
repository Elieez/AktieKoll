using AktieKoll.Data;
using AktieKoll.Models;
using AktieKoll.Services;
using Microsoft.EntityFrameworkCore;
using CsvHelper;
using System.Globalization;

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostgresConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("PostgresConnection environment variable not set");
    return;
}

var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
optionsBuilder.UseNpgsql(connectionString);

await using var context = new ApplicationDbContext(optionsBuilder.Options);

var httpClient = new HttpClient();
CsvReader csvReaderFactory(TextReader reader)
{
    var config = new CsvHelper.Configuration.CsvConfiguration(new CultureInfo("sv-SE"))
    {
        Delimiter = ";"
    };
    return new CsvReader(reader, config);
}

var csvService = new CsvFetchService(httpClient, csvReaderFactory);
var csvResults = await csvService.FetchInsiderTradesAsync();
var trades = csvResults.Select(dto => dto.ToInsiderTrade()).ToList();

var tradeService = new InsiderTradeService(context);
var message = await tradeService.AddInsiderTrades(trades);
Console.WriteLine(message);