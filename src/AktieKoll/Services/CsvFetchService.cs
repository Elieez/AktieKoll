using AktieKoll.Models;
using System.Globalization;
using CsvHelper; 
using CsvHelper.Configuration; 

namespace AktieKoll.Services;

public class CsvFetchService(HttpClient httpClient, Func<TextReader, CsvReader> csvReaderFactory)
{
    public async Task<List<CsvDTO>> FetchInsiderTradesAsync()
    {
        var utcToday = DateTime.UtcNow.Date;
        var utcYesterday = utcToday.AddDays(-1);

        var toDate = utcToday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var fromDate = utcYesterday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var url = $"https://marknadssok.fi.se/Publiceringsklient/sv-SE/Search/Search?SearchFunctionType=Insyn&Utgivare=&PersonILedandeSt%C3%A4llningNamn=&Transaktionsdatum.From=&Transaktionsdatum.To=&Publiceringsdatum.From={Uri.EscapeDataString(fromDate)}&Publiceringsdatum.To={Uri.EscapeDataString(toDate)}&button=export&Page=1";

        var csvData = await httpClient.GetStringAsync(url);

        using var reader = new StringReader(csvData);
        using var csv = csvReaderFactory(reader);

        var insiderTrades = csv.GetRecords<CsvDTO>().ToList();

        return insiderTrades;
    }
}
