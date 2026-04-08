using AktieKoll.Data;
using AktieKoll.Dtos;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Services;

public class FollowService(ApplicationDbContext context) : IFollowService
{
    private const int MaxFollows = 3;

    public async Task<ServiceResult<FollowStatusDto>> FollowAsync(string userId, int companyId, CancellationToken ct = default)
    {
        var user = await context.Users.FindAsync(new object[] { userId }, ct);
        if (user == null || !user.EmailConfirmed)
            return ServiceResult<FollowStatusDto>.Fail("Du måste verifiera din e-postadress för att bevaka bolag.", 403);

        var companyExists = await context.Companies.AnyAsync(c => c.Id == companyId, ct);
        if (!companyExists)
            return ServiceResult<FollowStatusDto>.Fail("Företaget hittades inte.", 404);

        var alreadyFollowing = await context.UserCompanyFollows
            .AnyAsync(f => f.UserId == userId && f.CompanyId == companyId, ct);

        if (alreadyFollowing)
        {
            var currentCount = await context.UserCompanyFollows.CountAsync(f => f.UserId == userId, ct);
            return ServiceResult<FollowStatusDto>.Ok(new FollowStatusDto { IsFollowing = true, FollowCount = currentCount });
        }

        var followCount = await context.UserCompanyFollows.CountAsync(f => f.UserId == userId, ct);
        if (followCount >= MaxFollows)
            return ServiceResult<FollowStatusDto>.Fail($"Du kan bevaka max {MaxFollows} bolag.", 400);

        context.UserCompanyFollows.Add(new UserCompanyFollow
        {
            UserId = userId,
            CompanyId = companyId,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(ct);

        return ServiceResult<FollowStatusDto>.Ok(new FollowStatusDto { IsFollowing = true, FollowCount = followCount + 1 });
    }

    public async Task<ServiceResult<FollowStatusDto>> UnfollowAsync(string userId, int companyId, CancellationToken ct = default)
    {
        var follow = await context.UserCompanyFollows
            .FirstOrDefaultAsync(f => f.UserId == userId && f.CompanyId == companyId, ct);

        if (follow is null)
        {
            var currentCount = await context.UserCompanyFollows.CountAsync(f => f.UserId == userId, ct);
            return ServiceResult<FollowStatusDto>.Ok(new FollowStatusDto { IsFollowing = false, FollowCount = currentCount });
        }

        context.UserCompanyFollows.Remove(follow);
        await context.SaveChangesAsync(ct);

        var newCount = await context.UserCompanyFollows.CountAsync(f => f.UserId == userId, ct);
        return ServiceResult<FollowStatusDto>.Ok(new FollowStatusDto { IsFollowing = false, FollowCount = newCount });
    }

    public async Task<IEnumerable<FollowedCompanyDto>> GetFollowedAsync(string userId, CancellationToken ct = default)
    {
        return await context.UserCompanyFollows
            .Where(f => f.UserId == userId)
            .Include(f => f.Company)
            .OrderBy(f => f.Company.Name)
            .Select(f => new FollowedCompanyDto
            {
                CompanyId = f.CompanyId,
                Code = f.Company.Code,
                Name = f.Company.Name,
                Isin = f.Company.Isin,
                FollowedSince = f.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<FollowStatusDto> GetFollowStatusAsync(string userId, int companyId, CancellationToken ct = default)
    {
        var isFollowing = await context.UserCompanyFollows
            .AnyAsync(f => f.UserId == userId &&  f.CompanyId == companyId, ct);

        var followCount = await context.UserCompanyFollows.CountAsync(f => f.UserId == userId, ct);

        return new FollowStatusDto { IsFollowing = isFollowing, FollowCount = followCount };
    }
}
