using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Services;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Globalization;
using static AktieKoll.Models.CsvDtoExtensions;

// Get Connection string from environment variable
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostgresConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("ConnectionStrings__PostgresConnection environment variable not set");
    return 1;
}

// Build host with DI
var builder = Host.CreateApplicationBuilder(args);

// Databse
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// HttpClients
builder.Services.AddHttpClient<CsvFetchService>();
builder.Services.AddHttpClient<OpenFigiService>(client =>
    client.BaseAddress = new Uri("https://api.openfigi.com/v3/"));

// CSV Reader Factory
builder.Services.AddSingleton<Func<TextReader, CsvReader>>(_ =>
{

    static CsvReader Factory(TextReader reader)
    {
        var config = new CsvHelper.Configuration.CsvConfiguration(new CultureInfo("sv-SE"))
        {
            Delimiter = ";"
        };
        return new CsvReader(reader, config);
    }
    return Factory;
});

// Services
builder.Services.AddTransient<CsvFetchService>();
builder.Services.AddTransient<IOpenFigiService, OpenFigiService>();
builder.Services.AddTransient<ISymbolService, SymbolService>();
builder.Services.AddTransient<InsiderTradeService>();

// Build and run 
var host = builder.Build();

using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;
var loggerFactory = services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("FetchTrades");

try
{
    logger.LogInformation("Starting insider trades fetch at {Time}", DateTime.UtcNow);

    var csvService = services.GetRequiredService<CsvFetchService>();
    var tradeService = services.GetRequiredService<InsiderTradeService>();

    var csvResults = await csvService.FetchInsiderTradesAsync();
    var trades = InsiderTradeMapper.MapDtosToTrades(csvResults);
    var message = await tradeService.AddInsiderTrades(trades);

    logger.LogInformation("Completed: {Message}", message);
    Console.WriteLine(message);

    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "Fatal error during fetch");
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}