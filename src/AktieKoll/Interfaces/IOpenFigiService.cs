namespace AktieKoll.Interfaces;

public interface IOpenFigiService
{
    Task<string?> GetTickerByIsinAsync(string isin);
}
