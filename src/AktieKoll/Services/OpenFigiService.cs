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
            var items = result?.FirstOrDefault()?.data ?? [];

            var best = items
                .OrderByDescending(i => string.Equals(i.exchCode, "SS", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(i => string.Equals(i.micCode, "XSTO", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(i => string.Equals(i.marketSector, "Equity", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            return best?.ticker;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("OpenFIGI request cancelled for {Isin}", isin);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve ticker for {isin}", isin);
            return null;
        }
    }

    private sealed class FigiMappingResponse
    {
        [JsonPropertyName("data")]
        public List<FigiMappingItem>? data { get; set; }
    }

    private sealed class FigiMappingItem
    {
        [JsonPropertyName("ticker")] public string? ticker { get; set; }
        [JsonPropertyName("exchCode")] public string? exchCode { get; set; }
        [JsonPropertyName("micCode")] public string? micCode { get; set; }
        [JsonPropertyName("marketSector")] public string? marketSector { get; set; }
    }
}