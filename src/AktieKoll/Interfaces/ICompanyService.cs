using AktieKoll.Models;

namespace AktieKoll.Interfaces;

public interface ICompanyService
{
    Task<IEnumerable<Company>> SearchCompaniesAsync(string query, int limit = 10);
    Task<Company?> GetCompanyByCodeAsync(string code);
    Task<Company?> GetCompanyByIsinAsync(string isin);
    Task<IEnumerable<Company>> GetAllCompaniesAsync();
}