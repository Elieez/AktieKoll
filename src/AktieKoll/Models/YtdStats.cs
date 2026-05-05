namespace AktieKoll.Models;

public class YtdStats
{
    public long TotalTransactions { get; set; }
    public decimal TotalValue { get; set; }
    public int UniqueCompanies { get; set; }
    public int TodayCount { get; set; }
    public decimal TodayValue { get; set; }
}
