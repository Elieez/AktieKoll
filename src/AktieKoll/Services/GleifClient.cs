using System.Text.Json;
using AktieKoll.Interfaces;

namespace AktieKoll.Services;

/// <summary>Free GLEIF API client: maps a LEI to the ISINs registered against it.</summary>
public class GleifClient(HttpClient http, ILogger<GleifClient> logger) : IGleifClient
{
    public async Task<IReadOnlyList<string>> GetIsinsAsync(string lei, CancellationToken ct)
    {
        try
        {
            var url = $"https://api.gleif.org/api/v1/lei-records/{Uri.EscapeDataString(lei)}/isins?page[size]=100";
            using var response = await http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var isins = new List<string>();
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    if (item.TryGetProperty("attributes", out var attrs)
                        && attrs.TryGetProperty("isin", out var isin)
                        && isin.ValueKind == JsonValueKind.String
                        && isin.GetString() is { Length: > 0 } value)
                    {
                        isins.Add(value);
                    }
                }
            }

            return isins;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "GLEIF ISIN lookup failed for LEI {Lei}", lei);
            return [];
        }
    }
}
