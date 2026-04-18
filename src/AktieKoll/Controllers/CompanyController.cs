using AktieKoll.Dtos;
using AktieKoll.Extensions;
using AktieKoll.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AktieKoll.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("public-api")]
public class CompanyController(ICompanyService service) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<CompanySearchResultDto>>> SearchCompanies(
        [FromQuery] string q,
        [FromQuery] int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
        {
            return BadRequest(new { error = "Query must be at least 2 characters long." });
        }

        if (limit < 1 || limit > 50)
        {
            return BadRequest(new { error = "Limit must be between 1 and 50" });
        }

        var companies = await service.SearchCompaniesAsync(q, limit);
        var dtos = companies.Select(c => c.ToSearchResultDto());

        return Ok(dtos);
    }

    [HttpGet("{code}")]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<CompanyDto>> GetCompanyByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new { error = "Code is required." });
        }

        var company = await service.GetCompanyByCodeAsync(code);

        if (company == null)
        {
            return NotFound(new { error = $"Company with code '{code}' not found." });
        }

        return Ok(company.ToDto());
    }

    [HttpGet]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<IEnumerable<CompanyDto>>> GetAllCompanies()
    {
        var companies = await service.GetAllCompaniesAsync();
        var dtos = companies.Select(c => c.ToDto());

        return Ok(dtos);
    }
}
