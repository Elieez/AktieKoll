using AktieKoll.Models;

namespace AktieKoll.Interfaces;

public interface INotificationService
{
    Task ProcessBatchNotificationsAsync(
        string BatchRunId,
        IEnumerable<InsiderTrade> newTrades,
        CancellationToken ct = default);
}