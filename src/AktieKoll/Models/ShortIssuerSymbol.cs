using System.ComponentModel.DataAnnotations;

namespace AktieKoll.Models;

public class ShortIssuerSymbol
{
    [MaxLength(20)]
    public string Lei { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Symbol { get; set; } = string.Empty;
}
