using AktieKoll.Dtos;
using AktieKoll.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AktieKoll.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("api")]
public class ShortPositionsController(IShortPositionService service) : ControllerBase
{
    [HttpGet("top")]
    public async Task<ActionResult<ShortTopListDto>> GetTop(
        [FromQuery] string sort = "highest",
        [FromQuery] int days = 30,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<ShortSort>(sort, ignoreCase: true, out var parsed)
            || !string.Equals(parsed.ToString(), sort?.Trim(), StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Sort must be one of: highest, increase, decrease." });

        if (days < 1 || days > 365)
            return BadRequest(new { error = "Days must be between 1 and 365." });

        if (limit < 1 || limit > 100)
            return BadRequest(new { error = "Limit must be between 1 and 100." });

        return Ok(await service.GetTopAsync(parsed, days, limit, ct));
    }

    [HttpGet("company")]
    public async Task<ActionResult<ShortCompanyDto>> GetCompany([FromQuery] string? symbol, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return BadRequest(new { error = "Symbol is required." });

        var dto = await service.GetCompanyAsync(symbol, ct);
        if (dto is null)
            return NotFound(new { error = $"No short positions found for symbol {symbol}." });

        return Ok(dto);
    }
}
