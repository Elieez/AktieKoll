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
    [HttpPost]
    public async Task<IActionResult> PostInsiderTrades([FromBody] List<InsiderTrade> insiderTrades)
    {
        if (insiderTrades == null || insiderTrades.Count == 0)
        {
            return BadRequest("No data provided.");
        }

        var result = await tradeService.AddInsiderTrades(insiderTrades);
        if (result == null)
        {
            return BadRequest("No new trades added.");
        }

        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<InsiderTrade>>> GetInsiderTrades()
    {
        var trades = await tradeService.GetInsiderTrades();
        return Ok(trades);
    }

    [HttpGet("page")]
    public async Task<ActionResult<IEnumerable<InsiderTrade>>> GetInsiderTradesPage([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest("Page and page size must be greater than zero.");
        }
        var trades = await tradeService.GetInsiderTradesPage(page, pageSize);
        return Ok(trades);
    }

    [HttpGet("top")]
    public async Task<ActionResult<IEnumerable<InsiderTrade>>> GetInsiderTradesTop()
    {
        var trades = await tradeService.GetInsiderTradesTop();
        return Ok(trades);
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
    public async Task<ActionResult<IEnumerable<InsiderTrade>>> GetByCompanyName(
        [FromQuery] string name,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 10)
    {
        var trades = await tradeService.GetInsiderTradesByCompany(name, skip, take);
        return Ok(trades);
    }
}