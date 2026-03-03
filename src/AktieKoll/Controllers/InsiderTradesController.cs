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
public class InsiderTradesController(IInsiderTradeService tradeService) : ControllerBase
{
    [HttpGet("page")]
    public async Task<ActionResult<IEnumerable<InsiderTradeListDto>>> GetInsiderTradesPage([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest("Page and page size must be greater than zero.");
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
        [FromQuery] string? companyName,
        [FromQuery] int days = 30,
        [FromQuery] int? top = 3)
    {
        var stats = await tradeService.GetTransactionCountBuy(companyName, days, top);
        return Ok(stats);
    }

    [HttpGet("count-sell")]
    public async Task<ActionResult<IEnumerable<CompanyTransactionStats>>> GetTransactionCountSell(
        [FromQuery] string? companyName,
        [FromQuery] int days = 30,
        [FromQuery] int? top = 3)
    {
        var stats = await tradeService.GetTransactionCountSell(companyName, days, top);
        return Ok(stats);
    }

    [HttpGet("company")]
    public async Task<ActionResult<IEnumerable<InsiderTradeListDto>>> GetByCompanyName(
        [FromQuery] string name,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 10)
    {
        var trades = await tradeService.GetInsiderTradesByCompany(name, skip, take);

        if (!trades.Any())
        {
            return NotFound(new { error = $"No trades found for company: {name}" });
        }

        var dtos = trades.Select(t => t.ToListDto());

        return Ok(dtos);
    }
}