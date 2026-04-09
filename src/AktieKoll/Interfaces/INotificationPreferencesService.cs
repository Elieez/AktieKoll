using AktieKoll.Dtos;

namespace AktieKoll.Interfaces;

public interface INotificationPreferencesService
{
    Task<NotificationPreferencesDto> GetAsync(string userId, CancellationToken ct = default);
    Task<ServiceResult<NotificationPreferencesDto>> UpdateAsync(string userId, UpdateNotificationPreferencesDto dto, CancellationToken ct = default);
}