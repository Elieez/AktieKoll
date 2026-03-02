namespace AktieKoll.Dtos;

public record CompanySearchResultDto
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Isin { get; init; }
}

public record CompanyDto
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Isin { get; init; }
    public string? Currency { get; init; }
    public string? Type { get; init; }
    public DateTime LastUpdated { get; init; }
}