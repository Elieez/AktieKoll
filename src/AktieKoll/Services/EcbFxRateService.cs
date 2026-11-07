using System.Globalization;
using System.Xml.Linq;
using AktieKoll.Interfaces;
using AktieKoll.Models;

namespace AktieKoll.Services;

/// <summary>
/// Converts trade values to SEK using the European Central Bank's free daily reference
/// rates (no API key, no cost). ECB quotes everything against EUR, so the SEK rate for a
/// currency X is EURSEK / EURX.
/// </summary>
public class EcbFxRateService(HttpClient httpClient, ILogger<EcbFxRateService> logger) : IFxRateService
{
    private const string Last90DaysUrl = "https://www.ecb.europa.eu/stats/eurofxref/eurofxref-hist-90d.xml";
    private const string FullHistoryUrl = "https://www.ecb.europa.eu/stats/eurofxref/eurofxref-hist.xml";
    private static readonly XNamespace Ns = "http://www.ecb.int/vocabulary/2002-08-01/eurofxref";

    // ECB publishes on banking days only; look back over weekends/holidays.
    private const int MaxLookbackDays = 7;

    private SortedList<DateOnly, Dictionary<string, decimal>>? _ratesByDate;
    private bool _loadedFullHistory;

    public async Task ApplyValueSekAsync(IReadOnlyCollection<InsiderTrade> trades, bool fullHistory = false)
    {
        var pending = trades
            .Where(t => t.ValueSek is null && !IsSek(t.Currency))
            .ToList();

        if (pending.Count == 0)
            return;

        if (!await EnsureLoadedAsync(fullHistory))
            return;

        var missing = 0;
        foreach (var trade in pending)
        {
            var rate = GetSekPerUnit(trade.Currency, DateOnly.FromDateTime(trade.TransactionDate));
            if (rate is null)
            {
                missing++;
                continue;
            }
            trade.ValueSek = Math.Round(trade.Price * trade.Shares * rate.Value, 2);
        }

        if (missing > 0)
            logger.LogWarning("No ECB FX rate found for {Missing} of {Total} non-SEK trades", missing, pending.Count);
    }

    internal decimal? GetSekPerUnit(string currency, DateOnly date)
    {
        if (_ratesByDate is null || _ratesByDate.Count == 0)
            return null;

        var code = currency.Trim().ToUpperInvariant();
        for (var d = date; d >= date.AddDays(-MaxLookbackDays); d = d.AddDays(-1))
        {
            if (_ratesByDate.TryGetValue(d, out var rates)
                && rates.TryGetValue("SEK", out var eurSek)
                && rates.TryGetValue(code, out var eurX)
                && eurX != 0)
            {
                return eurSek / eurX;
            }
        }
        return null;
    }

    private async Task<bool> EnsureLoadedAsync(bool fullHistory)
    {
        if (_ratesByDate is not null && (_loadedFullHistory || !fullHistory))
            return true;

        try
        {
            var url = fullHistory ? FullHistoryUrl : Last90DaysUrl;
            await using var stream = await httpClient.GetStreamAsync(url);
            var doc = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
            _ratesByDate = Parse(doc);
            _loadedFullHistory = fullHistory;
            logger.LogInformation("Loaded ECB FX rates for {Days} days", _ratesByDate.Count);
            return true;
        }
        catch (Exception ex)
        {
            // FX is enrichment only — never block ingestion because ECB is unreachable.
            logger.LogWarning(ex, "Could not load ECB FX rates; non-SEK trades will have no SEK value");
            return false;
        }
    }

    internal static SortedList<DateOnly, Dictionary<string, decimal>> Parse(XDocument doc)
    {
        var result = new SortedList<DateOnly, Dictionary<string, decimal>>();
        foreach (var day in doc.Descendants(Ns + "Cube").Where(c => c.Attribute("time") != null))
        {
            var date = DateOnly.ParseExact(day.Attribute("time")!.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["EUR"] = 1m };
            foreach (var c in day.Elements(Ns + "Cube"))
            {
                var code = c.Attribute("currency")?.Value;
                var rate = c.Attribute("rate")?.Value;
                if (code != null && decimal.TryParse(rate, NumberStyles.Number, CultureInfo.InvariantCulture, out var r))
                    rates[code] = r;
            }
            result[date] = rates;
        }
        return result;
    }

    private static bool IsSek(string? currency)
        => string.Equals(currency?.Trim(), "SEK", StringComparison.OrdinalIgnoreCase);
}
