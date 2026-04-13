using AktieKoll.Dtos;
using AktieKoll.Extensions;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AktieKoll.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("api")]
public class InsiderTradesController(IInsiderTradeService tradeService, ILogger<InsiderTradesController> logger) : ControllerBase
{
    private const int MaxPageSize = 100;

    [HttpGet("page")]
    public async Task<ActionResult<IEnumerable<InsiderTradeListDto>>> GetInsiderTradesPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1)
        {
            return BadRequest(new { error = "Page must be greater than zero." });
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            return BadRequest(new { error = $"Page size must be between 1 and {MaxPageSize}." });
        }

        var trades = await tradeService.GetInsiderTradesPage(page, pageSize);
        var dtos = trades.Select(t => t.ToListDto());

        return Ok(dtos);
    }

    [HttpGet("top")]
    public async Task<ActionResult<IEnumerable<InsiderTradeListDto>>> GetInsiderTradesTop()
    {
        var trades = await tradeService.GetInsiderTradesTop();
        var dtos = trades.Select(t => t.ToListDto());

        return Ok(dtos);
    }

    [HttpGet("count-buy")]
    public async Task<ActionResult<IEnumerable<CompanyTransactionStats>>> GetTransactionCountBuy(
        [FromQuery] string? symbol,
        [FromQuery] int days = 30,
        [FromQuery] int? top = 3)
    {
        if (days < 1 || days > 3650)
            return BadRequest(new { error = "Days must be between 1 and 3650." });

        if (top.HasValue && (top.Value < 1 || top.Value > 100))
            return BadRequest(new { error = "Top must be between 1 and 100." });

        var stats = await tradeService.GetTransactionCountBuy(symbol, days, top);
        return Ok(stats);
    }

    [HttpGet("count-sell")]
    public async Task<ActionResult<IEnumerable<CompanyTransactionStats>>> GetTransactionCountSell(
        [FromQuery] string? symbol,
        [FromQuery] int days = 30,
        [FromQuery] int? top = 3)
    {
        if (days < 1 || days > 3650)
            return BadRequest(new { error = "Days must be between 1 and 3650." });

        if (top.HasValue && (top.Value < 1 || top.Value > 100))
            return BadRequest(new { error = "Top must be between 1 and 100." });

        var stats = await tradeService.GetTransactionCountSell(symbol, days, top);
        return Ok(stats);
    }

    [HttpGet("company")]
    public async Task<ActionResult<IEnumerable<InsiderTradeListDto>>> GetByCompanyName(
        [FromQuery] string? symbol,
        [FromQuery] string? name,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 10)
    {
        if (skip < 0)
            return BadRequest(new { error = "Skip must be zero or greater." });

        if (take < 1 || take > MaxPageSize)
            return BadRequest(new { error = $"Take must be between 1 and {MaxPageSize}." });
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            var trades = (await tradeService.GetInsiderTradesBySymbol(symbol, skip, take)).ToList();
            
            if (trades.Count == 0)
            {
                return NotFound(new
                {
                    error = $"No insider trades found for symbol {symbol}."
                });
            }

            return Ok(trades.Select(t => t.ToListDto()));
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var safeName = name.Replace("\r", "", StringComparison.Ordinal)
                               .Replace("\n", "", StringComparison.Ordinal);
            logger.LogWarning(
                "Deprecated ?name= parameter used on /api/InsiderTrades/company (name='{Name}').", safeName);

            var trades = (await tradeService.GetInsiderTradesByCompany(name, skip, take)).ToList();

            if (trades.Count == 0)
                return NotFound(new { error = $"No trades found for company: {name}" });

            return Ok(trades.Select(t => t.ToListDto()));
        }

        return BadRequest(new { error = "Either 'symbol' or 'name' query parameter is required." });
    }

    [HttpGet("ytd-stats")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult<YtdStats>> GetYtdStats()
    {
        var stats = await tradeService.GetYtdTransactionStatsAsync();
        return Ok(stats);
    }
}