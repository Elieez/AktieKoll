namespace AktieKoll.Models;

public record InsiderTradeIngestionResult(string Message, List<InsiderTrade> NewTrades);