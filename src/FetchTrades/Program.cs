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
// Per-query SQL logging is noise in job logs.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// HttpClients
builder.Services.AddHttpClient<CsvFetchService>();
builder.Services.AddHttpClient<IFxRateService, EcbFxRateService>();
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
    var fxService = services.GetRequiredService<IFxRateService>();

    // Backfill mode: dotnet run -- --backfill 2025-01-01 2026-11-07
    // Re-downloads FI's register day by day and fills the detail columns (LEI, venue,
    // closely-associated / share-program flags, SEK value) on trades already stored.
    // Never inserts trades and never sends notifications.
    if (args.Length >= 1 && args[0] == "--backfill")
    {
        if (args.Length < 3
            || !DateTime.TryParseExact(args[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
            || !DateTime.TryParseExact(args[2], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to)
            || from > to)
        {
            Console.Error.WriteLine("Usage: --backfill <yyyy-MM-dd from> <yyyy-MM-dd to>");
            return 1;
        }

        int totalFromFi = 0, totalMatched = 0, totalUpdated = 0;
        var failedDays = new List<DateTime>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            // FI's export is capped at 1000 rows, so fetch one publishing day at a time.
            // FI occasionally resets connections: retry a few times, then skip the day.
            List<AktieKoll.Models.CsvDTO>? dayRows = null;
            for (var attempt = 1; attempt <= 4 && dayRows is null; attempt++)
            {
                try
                {
                    dayRows = await csvService.FetchInsiderTradesAsync(day, day);
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
                {
                    var wait = TimeSpan.FromSeconds(10 * attempt * attempt);
                    logger.LogWarning("FI fetch for {Day:yyyy-MM-dd} failed (attempt {Attempt}/4), retrying in {Wait}s",
                        day, attempt, wait.TotalSeconds);
                    if (attempt < 4) await Task.Delay(wait);
                }
            }

            if (dayRows is null)
            {
                failedDays.Add(day);
                continue;
            }

            if (dayRows.Count >= 1000)
                logger.LogWarning("FI export for {Day:yyyy-MM-dd} hit the 1000-row cap; some rows may be missed", day);

            var dayTrades = InsiderTradeMapper.MapDtosToTrades(dayRows);
            await fxService.ApplyValueSekAsync(dayTrades, fullHistory: true);
            var r = await tradeService.BackfillTradeDetailsAsync(dayTrades);
            totalFromFi += r.FromFi;
            totalMatched += r.Matched;
            totalUpdated += r.Updated;
            logger.LogInformation("Backfill {Day:yyyy-MM-dd}: {FromFi} trades in FI, {Matched} found in DB, {Updated} updated",
                day, r.FromFi, r.Matched, r.Updated);

            await Task.Delay(TimeSpan.FromMilliseconds(500)); // be polite to FI
        }

        logger.LogInformation(
            "Backfill completed: {FromFi} trades in FI, {Matched} found in DB ({Missing} not stored), {Updated} updated",
            totalFromFi, totalMatched, totalFromFi - totalMatched, totalUpdated);

        if (failedDays.Count > 0)
        {
            logger.LogError("Backfill could not fetch {Count} day(s), re-run for these: {Days}",
                failedDays.Count, string.Join(", ", failedDays.Select(d => d.ToString("yyyy-MM-dd"))));
            return 1;
        }
        return 0;
    }

    var csvResults = await csvService.FetchInsiderTradesAsync();
    var trades = InsiderTradeMapper.MapDtosToTrades(csvResults);
    await fxService.ApplyValueSekAsync(trades);
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