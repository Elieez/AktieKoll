namespace AktieKoll.Dtos;

public record InsiderTradeListDto
{
    public required string CompanyName { get; init; }
    public required string InsiderName { get; init; }
    public string? Position { get; init; }
    public required string TransactionType { get; init; }
    public required int Shares { get; init; }
    public required decimal Price { get; init; }
    public required string Currency { get; init; }
    public string? Symbol { get; init; }
    public required DateTime PublishingDate { get; init; }
    public required DateTime TransactionDate { get; init; }
}