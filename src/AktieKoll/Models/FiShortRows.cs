namespace AktieKoll.Models;

public record FiAggregateRow(string IssuerName, string Lei, decimal PositionPercent, DateTime LatestPositionDate);

public record FiPositionRow(
    string HolderName,
    string IssuerName,
    string Isin,
    decimal PositionPercent,
    bool BelowThreshold,
    DateTime PositionDate,
    string? Comment);

public class FiFormatException(string message) : Exception(message);
