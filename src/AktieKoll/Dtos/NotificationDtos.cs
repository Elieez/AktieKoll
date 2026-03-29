namespace AktieKoll.Dtos;

public record NotificationPreferencesDto
{
    public bool EmailEnabled { get; init; }
    public bool DiscordEnabled { get; init; }
    public string? DiscordWebhookUrl { get; init; }
}

public record UpdateNotificationPreferencesDto
{
    public bool EmailEnabled { get; init; }
    public bool DiscordEnabled { get; init; }
    public string? DiscordWebhookUrl { get; init; }
}

public record FollowedCompanyDto
{
    public required int CompanyId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Isin { get; init; }
    public DateTime FollowedSince { get; init; }
}

public record FollowStatusDto
{
    public bool IsFollowing { get; init; }
    public int FollowCount { get; init; }
}