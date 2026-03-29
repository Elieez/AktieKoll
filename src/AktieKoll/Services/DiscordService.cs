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

        var fields = trades.Select(t => new
        {
            name = $"{t.InsiderName} ({t.Position ?? "okänd roll"})",
            value = FormatTradeField(t),
            inline = false,
        }).Take(25).ToArray();

        var isBuy = trades.Any(t => t.TransactionType.Contains("förvärv", StringComparison.OrdinalIgnoreCase));
        var color = isBuy ? 5083979 : 15757389; // #4deba8 green / #f06b4d red

        var stockUrl = $"{FrontendUrl}/stocks/{Uri.EscapeDataString(companyCode)}";

        var embed = new
        {
            title = $"Insiderhandel: {companyName}",
            url = stockUrl,
            color,
            description = $"{trades.Count} ny(a) transaktion(er) regristrerade.",
            fields,
            footer = new { text = "AktieKoll · Insiderhandel" },
            timestamp = DateTime.UtcNow.ToString("o"),
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
            logger.LogError(ex, "Discord webhook failed for company {Code}", companyCode);
            return false;
        }
    }

    private static string FormatTradeField(InsiderTrade t)
    {
        var type = t.TransactionType.ToLower() switch
        {
            var s when s.Contains("förvärv") => "KÖP",
            var s when s.Contains("avyttring") => "SÄLJ",
            _ => t.TransactionType
        };

        var value = (t.Price * t.Shares).ToString("N0");
        return $"**{type}** · {t.Shares:N0} aktier @ {t.Price:N2} {t.Currency}\nVärde: {value} {t.Currency} · {t.TransactionDate:yyyy-MM-dd}";
    }
}
