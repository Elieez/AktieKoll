using System.Security.Claims;
using AktieKoll.Dtos;
using AktieKoll.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AktieKoll.Controllers;

[ApiController]
[Route("api/notification/preferences")]
[Authorize]
[EnableRateLimiting("api")]
public class NotificationPreferencesController(INotificationPreferencesService prefService) : ControllerBase
{
    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<ActionResult<NotificationPreferencesDto>> Get(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        return Ok(await prefService.GetAsync(userId, ct));
    }

    [HttpPut]
    public async Task<ActionResult<NotificationPreferencesDto>> Update(
        [FromBody] UpdateNotificationPreferencesDto dto, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) 
            return Unauthorized();

        var result = await prefService.UpdateAsync(userId, dto, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new { error = result.Error });

        return Ok(result.Value);
    }
}