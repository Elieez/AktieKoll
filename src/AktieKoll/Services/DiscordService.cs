using System.Text.Json.Serialization;
using AktieKoll.Interfaces;
using AktieKoll.Models;

namespace AktieKoll.Services;

public class DiscordService(HttpClient httpClient, IConfiguration config, ILogger<DiscordService> logger) : IDiscordService
{
    private string FrontendUrl => (config["Frontend:Url"] ?? "http://localhost:3000").TrimEnd('/');

    public async Task<bool> SendTradeNotificationAsync(
        string webhookUrl,
        string companyName,
        string companyCode,
        List<InsiderTrade> trades,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
            return false;

        var stockUrl = $"{FrontendUrl}/stocks/{Uri.EscapeDataString(companyCode)}";

        var isBuy = trades.Any(t => t.TransactionType.Contains("förvärv", StringComparison.OrdinalIgnoreCase));
        var color = isBuy ? 5083979 : 15757389;

        var count = trades.Count;
        var countText = count == 1 ? "1 ny transaktion" : $"{count} nya transaktioner";

        var fields = new List<object>
        {
            new { name = "━━━━━━━━━━━━━━━━━━━━━━", value = "** **", inline = false }
        };

        fields.AddRange(trades.Take(25).Select(t => (object)new
        {
            name = FormatTradeTitle(t),
            value = FormatTradeField(t),
            inline = false
        }));

        var embed = new DiscordEmbed
        {
            Author = new DiscordAuthor
            {
                Name = "AktieKoll",
                IconUrl = "https://raw.githubusercontent.com/Elieez/aktiekollwebb/master/public/aktiekoll_discord_avatar.png"
            },
            Title = $"📈 Insiderhandel · {companyName}",
            Url = stockUrl,
            Color = color,
            Description = $"**{countText}** registrerad(e) på Stockholmsbörsen.",
            Fields = fields,
            Footer = new DiscordFooter { Text = "AktieKoll · Insiderhandel · Stockholmsbörsen" },
            Timestamp = DateTime.UtcNow.ToString("o")
        };

        var payload = new { embeds = new[] { embed } };

        try
        {
            var response = await httpClient.PostAsJsonAsync(webhookUrl, payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning(
                    "Discord webhook returned {Status} for company {Code}: {Body}",
                    response.StatusCode, companyCode, body);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("Discord webhook request failed for company {Code}: {ExceptionType}", companyCode, ex.GetType().Name);
            return false;
        }
    }

    private static string FormatTradeTitle(InsiderTrade t)
    {
        var isBuy = t.TransactionType.Contains("förvärv", StringComparison.OrdinalIgnoreCase);
        var icon = isBuy ? "🟢" : "🔴";
        return $"{icon} {t.InsiderName}";
    }

    private static string FormatTradeField(InsiderTrade t)
    {
        var type = t.TransactionType.ToLower() switch
        {
            var s when s.Contains("förvärv") => "Köp",
            var s when s.Contains("avyttring") => "Sälj",
            _ => t.TransactionType
        };

        var value = (t.Price * t.Shares).ToString("N0");

        return $"> **Roll:** {t.Position ?? "Okänd roll"}\n" +
               $"> **Typ:** {type}\n" +
               $"> **Antal:** {t.Shares:N0} aktier @ {t.Price:N2} {t.Currency}\n" +
               $"> **Värde:** {value} {t.Currency}\n" +
               $"> 📅 {t.TransactionDate:yyyy-MM-dd}";
    }

    private class DiscordEmbed
    {
        [JsonPropertyName("author")]
        public DiscordAuthor? Author { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("color")]
        public int Color { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("fields")]
        public List<object>? Fields { get; set; }

        [JsonPropertyName("footer")]
        public DiscordFooter? Footer { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }
    }

    private class DiscordAuthor
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("icon_url")]
        public string? IconUrl { get; set; }
    }

    private class DiscordFooter
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}