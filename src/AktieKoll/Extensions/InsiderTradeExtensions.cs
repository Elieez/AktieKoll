using AktieKoll.Models;

namespace AktieKoll.Extensions;

public static class InsiderTradeExtensions
{
    public static InsiderTrade? FindDuplicate(this IEnumerable<InsiderTrade> trades, InsiderTrade trade)
        => trades.FirstOrDefault(t =>
            t.CompanyName == trade.CompanyName &&
            t.InsiderName == trade.InsiderName &&
            t.Position == trade.Position &&
            t.TransactionType == trade.TransactionType &&
            t.Shares == trade.Shares &&
            t.Price == trade.Price &&
            t.PublishingDate == trade.PublishingDate
        );

    public static bool IsRevised(this InsiderTrade trade)
        => string.Equals(trade.Status, "Reviderad", StringComparison.OrdinalIgnoreCase);
}
