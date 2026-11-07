using AktieKoll.Models;

namespace AktieKoll.Interfaces;

public interface IFxRateService
{
    /// <summary>
    /// Sets <see cref="InsiderTrade.ValueSek"/> on trades in a non-SEK currency, using the
    /// FX rate on (or the closest banking day before) the transaction date.
    /// Trades whose rate can't be found keep ValueSek = null. Never throws on network errors.
    /// </summary>
    /// <param name="fullHistory">Load the full ECB history (needed for backfills older than 90 days).</param>
    Task ApplyValueSekAsync(IReadOnlyCollection<InsiderTrade> trades, bool fullHistory = false);
}
