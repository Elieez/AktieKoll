using AktieKoll.Dtos;

namespace AktieKoll.Interfaces;

public enum ShortSort { Highest, Increase, Decrease }

public interface IShortPositionService
{
    Task<ShortTopListDto> GetTopAsync(ShortSort sort, int days, int limit, CancellationToken ct);
    Task<ShortCompanyDto?> GetCompanyAsync(string symbol, CancellationToken ct);
}
