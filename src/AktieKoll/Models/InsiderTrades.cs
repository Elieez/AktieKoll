using System.ComponentModel.DataAnnotations.Schema;

namespace AktieKoll.Models;

public class InsiderTrade
{
    public int Id { get; set; }
    public required string CompanyName { get; set; } 
    public required string InsiderName { get; set; }
    public string? Position { get; set; }
    public required string TransactionType { get; set; }
    public required int Shares { get; set; }
    public required decimal Price { get; set; }

    [Column(TypeName = "date")]
    public required DateTime Date { get; set; }
}
