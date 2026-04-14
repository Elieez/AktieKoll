using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Services;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Globalization;
using static AktieKoll.Extensions.CsvDtoExtensions;

// Get Connection string from environment variable
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostgresConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("ConnectionStrings__PostgresConnection environment variable not set");
    return 1;
}

// Build host with DI
var builder = Host.CreateApplicationBuilder(args);

// Suppress HttpClient request/response URL logging to avoid leaking webhook URLs and tokens in CI logs
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// HttpClients
builder.Services.AddHttpClient<CsvFetchService>();
builder.Services.AddHttpClient<IDiscordService, DiscordService>();

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
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTransient<CsvFetchService>();
builder.Services.AddTransient<ISymbolService, SymbolService>();
builder.Services.AddTransient<InsiderTradeService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Build and run
var host = builder.Build();

using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;
var loggerFactory = services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("FetchTrades");

try
{
    logger.LogInformation("Starting insider trades fetch at {Time}", DateTime.UtcNow);

    var batchRunId = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm");

    var csvService = services.GetRequiredService<CsvFetchService>();
    var tradeService = services.GetRequiredService<InsiderTradeService>();
    var notificationService = services.GetRequiredService<INotificationService>();

    var csvResults = await csvService.FetchInsiderTradesAsync();
    var trades = InsiderTradeMapper.MapDtosToTrades(csvResults);
    var result = await tradeService.AddInsiderTrades(trades);

    logger.LogInformation("Completed: {Message}", result.Message);

    if (result.NewTrades.Count > 0)
    {
        logger.LogInformation(
            "Processing notifications for {Count} new trades (batch {BatchRunId})",
            result.NewTrades.Count, batchRunId);

        await notificationService.ProcessBatchNotificationsAsync(batchRunId, result.NewTrades);
    }

    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "Fatal error during fetch");
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}