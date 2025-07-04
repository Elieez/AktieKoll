﻿using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.AspNetCore.Mvc;

namespace AktieKoll.Controllers;

[ApiController]
[Route("api/[controller]")]
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

    [HttpGet("top")]
    public async Task<ActionResult<IEnumerable<InsiderTrade>>> GetInsiderTradesTop()
    {
        var trades = await tradeService.GetInsiderTradesTop();
        return Ok(trades);
    }

    [HttpGet("top-companies")]
    public async Task<ActionResult<IEnumerable<CompanyTransactionStats>>> GetTopCompanies()
    {
        var stats = await tradeService.GetTopCompaniesByTransactions();
        return Ok(stats);
    }
}
