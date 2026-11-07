namespace AktieKoll.Models;

public class TradedSymbol
{
    public required string Symbol { get; set; }
    public required DateTime LastTradeDate { get; set; }
}
