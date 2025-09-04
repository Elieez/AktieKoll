using AktieKoll.Interfaces;

namespace AktieKoll.Services;

public class OpenFigiService(HttpClient httpClient, ILogger<OpenFigiService> logger) : IOpenFigiService
{
    public async Task<string?> GetTickerByIsinAsync(string isin)
    {
        try
        {
            var request = new[] { new { idType = "ID_ISIN", idValue = isin } };
            var response = await httpClient.PostAsJsonAsync("mapping", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<List<FigiMappingResponse>>();
            return result?.FirstOrDefault()?.data?.FirstOrDefault()?.ticker;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve ticker for {isin}", isin);
            return null;
        }
    }

    private class FigiMappingResponse
    {
        public List<FigiMappingData>? data { get; set; }
    }

    private class FigiMappingData
    {
        public string? ticker { get; set; }
    }
}
