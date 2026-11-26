namespace AktieKoll.Dtos;

public record ShortListItemDto(
    string IssuerName,
    string Lei,
    string? Symbol,
    decimal PositionPercent,
    DateTime? LatestPositionDate,
    decimal? Change,
    DateTime? ChangeSince,
    int HolderCount,
    int InsiderBuys90d,
    int InsiderSells90d);

public record ShortTopListDto(DateTime? UpdatedAt, IReadOnlyList<ShortListItemDto> Items);

public record ShortHolderDto(string HolderName, decimal PositionPercent, DateTime PositionDate);

public record SeriesPointDto(DateTime Date, decimal Value);

public record ShortCompanyDto(
    string IssuerName,
    string Lei,
    string? Symbol,
    decimal PositionPercent,
    DateTime? LatestPositionDate,
    decimal? Change,
    DateTime? ChangeSince,
    IReadOnlyList<ShortHolderDto> Holders,
    IReadOnlyList<SeriesPointDto> TotalSeries,
    IReadOnlyList<SeriesPointDto> SignificantSeries);
