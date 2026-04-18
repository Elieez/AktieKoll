using AktieKoll.Dtos;

namespace AktieKoll.Interfaces;

public interface IFollowService
{
    Task<ServiceResult<FollowStatusDto>> FollowAsync(string userId, int companyId, CancellationToken ct = default);
    Task<ServiceResult<FollowStatusDto>> UnfollowAsync(string userId, int companyId, CancellationToken ct = default);
    Task<IEnumerable<FollowedCompanyDto>> GetFollowedAsync(string userId, CancellationToken ct = default);
    Task<FollowStatusDto> GetFollowStatusAsync(string userId, int companyId, CancellationToken ct = default);
}