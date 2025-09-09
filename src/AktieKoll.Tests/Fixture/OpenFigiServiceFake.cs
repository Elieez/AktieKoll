using AktieKoll.Interfaces;

namespace AktieKoll.Tests.Fixture;

public class OpenFigiServiceFake : IOpenFigiService
{
    private readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase);

    public OpenFigiServiceFake Map(string isin, string ticker)
    {
        _map[isin] = ticker;
        return this;
    }

    public Task<string?> GetTickerByIsinAsync(string isin) 
        => Task.FromResult(_map.TryGetValue(isin, out var ticker) ? ticker : null);
}
