﻿using AktieKoll.Models;
using System.Globalization;
using System.Text;
using CsvHelper;

namespace AktieKoll.Services;

public class CsvFetchService(HttpClient httpClient, Func<TextReader, CsvReader> csvReaderFactory)
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

        using var stream = await httpClient.GetStreamAsync(url);
        using var reader = new StreamReader(stream, Encoding.Unicode, detectEncodingFromByteOrderMarks: false);
        using var csv = csvReaderFactory(reader);
        var insiderTrades = csv.GetRecords<CsvDTO>().ToList();

        return insiderTrades;
    }
}