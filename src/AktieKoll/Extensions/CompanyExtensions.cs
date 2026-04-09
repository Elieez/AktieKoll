using AktieKoll.Dtos;
using AktieKoll.Models;

namespace AktieKoll.Extensions;

public static class CompanyExtensions
{
    public static CompanySearchResultDto ToSearchResultDto(this Company company)
    {
        return new CompanySearchResultDto
        {
            Code = company.Code,
            Name = company.Name,
            Isin = company.Isin
        };
    }

    public static CompanyDto ToDto(this Company company)
    {
        return new CompanyDto
        {
            Id = company.Id,
            Code = company.Code,
            Name = company.Name,
            Isin = company.Isin,
            Currency = company.Currency,
            Type = company.Type,
            LastUpdated = company.LastUpdated
        };
    }
}
