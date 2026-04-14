using AktieKoll.Models;

namespace AktieKoll.Interfaces;

public interface IDiscordService
{
    Task<bool> SendTradeNotificationAsync(string webhookUrl,string companyName, string companyCode, List<InsiderTrade> trades,CancellationToken ct = default);
}
