namespace AktieKoll.Models;

public class InsiderTrade
{
    public int Id { get; set; }
    public string CompanyName { get; set; }
    public string InsiderName { get; set; }
    public string Position { get; set; }
    public string TransactionType { get; set; }
    public int Shares { get; set; }
    public decimal Price { get; set; }
    public DateTime Date { get; set; }
}
