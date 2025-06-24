using AktieKoll.Data;
using AktieKoll.Services;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;
using static AktieKoll.Models.CsvDtoExtensions;

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

var logger = NullLogger<CsvFetchService>.Instance;
var csvService = new CsvFetchService(httpClient, csvReaderFactory, logger);

var csvResults = await csvService.FetchInsiderTradesAsync();
var trades = InsiderTradeMapper.MapDtosToTrades(csvResults);

var tradeService = new InsiderTradeService(context);
var message = await tradeService.AddInsiderTrades(trades);

Console.WriteLine(message);