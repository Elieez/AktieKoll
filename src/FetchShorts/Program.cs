using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using AktieKoll.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostgresConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("ConnectionStrings__PostgresConnection environment variable not set");
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddHttpClient<FiShortDownloader>();
builder.Services.AddHttpClient<IGleifClient, GleifClient>();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTransient<ISymbolService, SymbolService>();
builder.Services.AddTransient<ShortIssuerResolver>();
builder.Services.AddTransient<ShortIngestionService>();

var host = builder.Build();

using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;
var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("FetchShorts");

try
{
    logger.LogInformation("Starting short positions fetch at {Time}", DateTime.UtcNow);

    var downloader = services.GetRequiredService<FiShortDownloader>();

    // Download everything before touching the database.
    await using var aggregateStream = await downloader.DownloadAsync(FiShortFile.Aggregate, CancellationToken.None);
    await using var currentStream = await downloader.DownloadAsync(FiShortFile.Current, CancellationToken.None);
    await using var historyStream = await downloader.DownloadAsync(FiShortFile.History, CancellationToken.None);

    IReadOnlyList<FiAggregateRow> aggregate;
    IReadOnlyList<FiPositionRow> current, history;
    try
    {
        aggregate = FiShortFileParser.ParseAggregate(aggregateStream);
        current = FiShortFileParser.ParsePositions(currentStream);
        history = FiShortFileParser.ParsePositions(historyStream);
    }
    catch (FiFormatException ex)
    {
        logger.LogError(ex, "FI short file format changed");
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }

    var ingestion = services.GetRequiredService<ShortIngestionService>();
    var result = await ingestion.IngestAsync(aggregate, current, history, CancellationToken.None);

    logger.LogInformation(
        "Completed: {NewPositions} new positions, {Snapshots} snapshots, {DroppedOut} dropped out, {UnresolvedIssuers} unresolved issuers",
        result.NewPositions, result.Snapshots, result.DroppedOut, result.UnresolvedIssuers);
    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "Fatal error during short fetch");
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
