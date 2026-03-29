using System.Security.Claims;
using AktieKoll.Dtos;
using AktieKoll.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AktieKoll.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("api")]
public class FollowController(IFollowService followService) : ControllerBase
{
    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpPost("{companyId:int}")]
    public async Task<IActionResult> Follow(int companyId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) 
            return Unauthorized();

        var result = await followService.FollowAsync(userId, companyId, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new { error = result.Error });

        return Ok(result.Value);
    }

    [HttpDelete("{companyId:int}")]
    public async Task<IActionResult> Unfollow(int companyId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) 
            return Unauthorized();

        var result = await followService.UnfollowAsync(userId, companyId, ct);
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FollowedCompanyDto>>> GetFollowed(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        return Ok(await followService.GetFollowedAsync(userId, ct));
    }

    [HttpGet("{CompanyId:int}")]
    public async Task<ActionResult<FollowStatusDto>> GetFollowStatus(int companyId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        return Ok(await followService.GetFollowStatusAsync(userId, companyId, ct));
    }
}
