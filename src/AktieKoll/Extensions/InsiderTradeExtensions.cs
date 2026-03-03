using AktieKoll.Dtos;
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

    public static InsiderTradeListDto ToListDto(this InsiderTrade trade)
    {
        return new InsiderTradeListDto
        {
            CompanyName = trade.CompanyName,
            InsiderName = trade.InsiderName,
            Position = trade.Position,
            TransactionType = trade.TransactionType,
            Shares = trade.Shares,
            Price = trade.Price,
            Currency = trade.Currency,
            Symbol = trade.Symbol,
            PublishingDate = trade.PublishingDate,
            TransactionDate = trade.TransactionDate
        };
    }
}
