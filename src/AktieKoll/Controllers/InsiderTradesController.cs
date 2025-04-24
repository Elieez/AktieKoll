using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.AspNetCore.Mvc;

namespace AktieKoll.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InsiderTradesController : ControllerBase
{
    private readonly IInsiderTradeService _tradeService;

    public InsiderTradesController(IInsiderTradeService tradeService)
    {
        _tradeService = tradeService;
    }

    [HttpPost]
    public async Task<IActionResult> PostInsiderTrades([FromBody] List<InsiderTrade> insiderTrades)
    {
        if (insiderTrades == null || insiderTrades.Count == 0)
        {
            return BadRequest("No data provided.");
        }

        var result = await _tradeService.AddInsiderTrades(insiderTrades);
        if (result == null)
        {
           return BadRequest("No new trades added.");
        }

        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<InsiderTrade>>> GetInsiderTrades()
    {
        var trades = await _tradeService.GetInsiderTrades();
        return Ok(trades);
    }
}
