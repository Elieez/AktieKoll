using AktieKoll.Data;
using AktieKoll.Models;
using AktieKoll.Services;
using AktieKoll.Tests.Shared.TestHelpers;

namespace AktieKoll.Tests.Unit;

public class FollowServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(ApplicationDbContext ctx, Company company)> SetupWithCompany(
        bool emailConfirmed = true)
    {
        var ctx = ServiceTestHelpers.CreateContext();
        ctx.Users.Add(new ApplicationUser
        {
            Id                 = "user1",
            UserName           = "test@example.com",
            NormalizedUserName = "TEST@EXAMPLE.COM",
            Email              = "test@example.com",
            NormalizedEmail    = "TEST@EXAMPLE.COM",
            SecurityStamp      = "stamp",
            ConcurrencyStamp   = "stamp",
            EmailConfirmed     = emailConfirmed,
            DisplayName        = "Test User",
            CreatedAt          = DateTime.UtcNow
        });
        var company = new Company { Code = "VOLV-B", Name = "Volvo B", Isin = null, Currency = "SEK", Type = "Common Stock" };
        ctx.Companies.Add(company);
        await ctx.SaveChangesAsync();
        return (ctx, company);
    }

    [Fact]
    public async Task Follow_NewFollow_ReturnsIsFollowingTrue()
    {
        var (ctx, company) = await SetupWithCompany();
        var service = new FollowService(ctx);

        var result = await service.FollowAsync("user1", company.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsFollowing);
        Assert.Equal(1, result.Value.FollowCount);
    }

    [Fact]
    public async Task Follow_AlreadyFollowing_IsIdempotent()
    {
        var (ctx, company) = await SetupWithCompany();
        ctx.UserCompanyFollows.Add(new UserCompanyFollow { UserId = "user1", CompanyId = company.Id, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync(Ct);
        var service = new FollowService(ctx);

        var result = await service.FollowAsync("user1", company.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsFollowing);
        Assert.Equal(1, result.Value.FollowCount);
    }

    [Fact]
    public async Task Follow_CompanyNotFound_Returns404()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        ctx.Users.Add(new ApplicationUser
        {
            Id = "user1", UserName = "t@t.se", NormalizedUserName = "T@T.SE",
            Email = "t@t.se", NormalizedEmail = "T@T.SE",
            SecurityStamp = "s", ConcurrencyStamp = "s",
            EmailConfirmed = true, DisplayName = "T", CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync(Ct);
        var service = new FollowService(ctx);

        var result = await service.FollowAsync("user1", 999, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Follow_MaxFollowsReached_Returns400()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        ctx.Users.Add(new ApplicationUser
        {
            Id = "user1", UserName = "t@t.se", NormalizedUserName = "T@T.SE",
            Email = "t@t.se", NormalizedEmail = "T@T.SE",
            SecurityStamp = "s", ConcurrencyStamp = "s",
            EmailConfirmed = true, DisplayName = "T", CreatedAt = DateTime.UtcNow
        });

        // Add 4 companies, follow 3 of them (max), then try to follow the 4th
        var companies = new List<Company>();
        for (var i = 0; i < 4; i++)
        {
            var c = new Company { Code = $"CO{i}", Name = $"Company {i}", Isin = null, Currency = "SEK", Type = "Common Stock" };
            ctx.Companies.Add(c);
            companies.Add(c);
        }
        await ctx.SaveChangesAsync(Ct);

        for (var i = 0; i < 3; i++)
        {
            ctx.UserCompanyFollows.Add(new UserCompanyFollow { UserId = "user1", CompanyId = companies[i].Id, CreatedAt = DateTime.UtcNow });
        }
        await ctx.SaveChangesAsync(Ct);

        var service = new FollowService(ctx);

        var result = await service.FollowAsync("user1", companies[3].Id, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Follow_UnverifiedUser_Returns403()
    {
        var (ctx, company) = await SetupWithCompany(emailConfirmed: false);
        var service = new FollowService(ctx);

        var result = await service.FollowAsync("user1", company.Id, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task Unfollow_Existing_ReturnsIsFollowingFalse()
    {
        var (ctx, company) = await SetupWithCompany();
        ctx.UserCompanyFollows.Add(new UserCompanyFollow { UserId = "user1", CompanyId = company.Id, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync(Ct);
        var service = new FollowService(ctx);

        var result = await service.UnfollowAsync("user1", company.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsFollowing);
        Assert.Equal(0, result.Value.FollowCount);
    }

    [Fact]
    public async Task Unfollow_NotFollowing_IsIdempotent()
    {
        var (ctx, company) = await SetupWithCompany();
        var service = new FollowService(ctx);

        var result = await service.UnfollowAsync("user1", company.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsFollowing);
        Assert.Equal(0, result.Value.FollowCount);
    }

    [Fact]
    public async Task GetFollowed_ReturnsAllFollowedCompanies()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        ctx.Users.Add(new ApplicationUser
        {
            Id = "user1", UserName = "t@t.se", NormalizedUserName = "T@T.SE",
            Email = "t@t.se", NormalizedEmail = "T@T.SE",
            SecurityStamp = "s", ConcurrencyStamp = "s",
            EmailConfirmed = true, DisplayName = "T", CreatedAt = DateTime.UtcNow
        });
        var c1 = new Company { Code = "ERIC-B", Name = "Ericsson B", Isin = null, Currency = "SEK", Type = "Common Stock" };
        var c2 = new Company { Code = "VOLV-B", Name = "Volvo B", Isin = null, Currency = "SEK", Type = "Common Stock" };
        ctx.Companies.AddRange(c1, c2);
        await ctx.SaveChangesAsync(Ct);
        ctx.UserCompanyFollows.AddRange(
            new UserCompanyFollow { UserId = "user1", CompanyId = c1.Id, CreatedAt = DateTime.UtcNow },
            new UserCompanyFollow { UserId = "user1", CompanyId = c2.Id, CreatedAt = DateTime.UtcNow }
        );
        await ctx.SaveChangesAsync(Ct);
        var service = new FollowService(ctx);

        var followed = (await service.GetFollowedAsync("user1", Ct)).ToList();

        Assert.Equal(2, followed.Count);
        Assert.Contains(followed, f => f.Code == "ERIC-B");
        Assert.Contains(followed, f => f.Code == "VOLV-B");
    }

    [Fact]
    public async Task GetFollowStatus_ReturnsCorrectStatus()
    {
        var (ctx, company) = await SetupWithCompany();
        ctx.UserCompanyFollows.Add(new UserCompanyFollow { UserId = "user1", CompanyId = company.Id, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync(Ct);
        var service = new FollowService(ctx);

        var status = await service.GetFollowStatusAsync("user1", company.Id, Ct);

        Assert.True(status.IsFollowing);
        Assert.Equal(1, status.FollowCount);
    }
}
