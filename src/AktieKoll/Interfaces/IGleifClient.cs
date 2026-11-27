namespace AktieKoll.Interfaces;

public interface IGleifClient
{
    /// <summary>ISINs GLEIF links to the given LEI. Returns an empty list on any failure.</summary>
    Task<IReadOnlyList<string>> GetIsinsAsync(string lei, CancellationToken ct);
}
