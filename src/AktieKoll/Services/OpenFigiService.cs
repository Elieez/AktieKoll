using AktieKoll.Interfaces;
using System.Text.Json.Serialization;

namespace AktieKoll.Services;

public class OpenFigiService(HttpClient httpClient, ILogger<OpenFigiService> logger) : IOpenFigiService
{
    public async Task<string?> GetTickerByIsinAsync(string isin, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(isin)) return null;

        try
        {
            var request = new[] { new { idType = "ID_ISIN", idValue = isin } };

            using var response = await httpClient.PostAsJsonAsync("mapping", request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<List<FigiMappingResponse>>(cancellationToken: ct);
            var items = result?.FirstOrDefault()?.Data ?? [];

            var best = items
                .OrderByDescending(i => string.Equals(i.ExchCode, "SS", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(i => string.Equals(i.MicCode, "XSTO", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(i => string.Equals(i.MarketSector, "Equity", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            return best?.Ticker;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("OpenFIGI request cancelled for {Isin}", isin);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve ticker for {Isin}", isin);
            return null;
        }
    }

    private sealed class FigiMappingResponse
    {
        [JsonPropertyName("data")]
        public List<FigiMappingItem>? Data { get; set; }
    }

    private sealed class FigiMappingItem
    {
        [JsonPropertyName("ticker")] public string? Ticker { get; set; }
        [JsonPropertyName("exchCode")] public string? ExchCode { get; set; }
        [JsonPropertyName("micCode")] public string? MicCode { get; set; }
        [JsonPropertyName("marketSector")] public string? MarketSector { get; set; }
    }
}