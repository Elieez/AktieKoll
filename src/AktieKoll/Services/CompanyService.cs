using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Services;

public class CompanyService(ApplicationDbContext context) : ICompanyService
{
    public async Task<IEnumerable<Company>> SearchCompaniesAsync(string query, int limit = 10)
    {
        if (string.IsNullOrEmpty(query) || query.Length < 2)
            return [];

        var searchTerm = query.ToLower().Trim();

        var companies = await context.Companies
            .Where(c =>
                c.Code.ToLower().Contains(searchTerm) ||
                c.Name.ToLower().Contains(searchTerm))
            .ToListAsync();

        return companies
            .OrderByDescending(c => 
                c.Code.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                c.Name.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase)) 
            .ThenBy(c => c.Code)
            .ThenBy(c => c.Name)
            .Take(limit);
    }

    public async Task<Company?> GetCompanyByCodeAsync(string code)
    {
        return await context.Companies
            .FirstOrDefaultAsync(c => c.Code.ToLower() == code.ToLower());
    }

    public async Task<Company?> GetCompanyByIsinAsync(string isin)
    {
        return await context.Companies
            .FirstOrDefaultAsync(c => c.Isin == isin);
    }

    public async Task<IEnumerable<Company>> GetAllCompaniesAsync()
    {
        return await context.Companies
            .OrderBy(c => c.Name)
            .ToListAsync();
    }
}