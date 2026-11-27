using Microsoft.Extensions.Logging;

namespace AktieKoll.Services;

public enum FiShortFile { Aggregate, Current, History }

public class FiShortDownloader(HttpClient http, ILogger<FiShortDownloader> logger)
{
    private const int MaxAttempts = 4;
    private const string UserAgent = "AktieKoll/1.0 (+https://aktiekoll.com)";
    private const string BaseUrl = "https://www.fi.se/sv/vara-register/blankningsregistret/";

    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(10);

    private static string UrlFor(FiShortFile file) => file switch
    {
        FiShortFile.Aggregate => BaseUrl + "GetBlankningsregisterAggregat/",
        FiShortFile.Current => BaseUrl + "GetAktuellFile/",
        FiShortFile.History => BaseUrl + "GetHistFile/",
        _ => throw new ArgumentOutOfRangeException(nameof(file))
    };

    public async Task<MemoryStream> DownloadAsync(FiShortFile file, CancellationToken ct)
    {
        var url = UrlFor(file);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                var ms = new MemoryStream();
                await response.Content.CopyToAsync(ms, ct);
                ms.Position = 0;
                return ms;
            }
            catch (Exception ex) when (
                !ct.IsCancellationRequested
                && ex is HttpRequestException or IOException or TaskCanceledException)
            {
                if (attempt >= MaxAttempts)
                {
                    logger.LogError(ex, "FI {File} download failed after {Attempts} attempts", file, MaxAttempts);
                    throw;
                }

                var wait = BaseDelay * (attempt * attempt);
                logger.LogWarning(ex, "FI {File} download failed (attempt {Attempt}/{Max}), retrying in {Wait}s",
                    file, attempt, MaxAttempts, wait.TotalSeconds);
                await Task.Delay(wait, ct);
            }
        }
    }
}
