using AktieKoll.Models;
using System.Globalization;
using System.Text;
using CsvHelper;

namespace AktieKoll.Services;

public class CsvFetchService(HttpClient httpClient, Func<TextReader, CsvReader> csvReaderFactory, ILogger<CsvFetchService> logger)
{
    public async Task<List<CsvDTO>> FetchInsiderTradesAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var utcToday = DateTime.UtcNow.Date;
        var utcYesterday = utcToday.AddDays(-1);

        var from = fromDate ?? utcYesterday;
        var to = toDate ?? utcToday;

        var fromDateString = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var toDateString = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var url = $"https://marknadssok.fi.se/Publiceringsklient/sv-SE/Search/Search?SearchFunctionType=Insyn&Utgivare=&PersonILedandeSt%C3%A4llningNamn=&Transaktionsdatum.From=&Transaktionsdatum.To=&Publiceringsdatum.From={Uri.EscapeDataString(fromDateString)}&Publiceringsdatum.To={Uri.EscapeDataString(toDateString)}&button=export&Page=1";

        logger.LogInformation("Fetching insider trades from {From} to {To}", fromDateString, toDateString);
        logger.LogDebug("Request URL: {Url}", url);
        try
        {
            using var stream = await httpClient.GetStreamAsync(url);
            using var reader = new StreamReader(stream, Encoding.Unicode, detectEncodingFromByteOrderMarks: false);
            using var csv = csvReaderFactory(reader);
            var insiderTrades = csv.GetRecords<CsvDTO>().ToList();

            logger.LogInformation("Fetched {Count} insider trades", insiderTrades.Count);

            return insiderTrades;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch insider trades");
            throw;
        }
    }
}