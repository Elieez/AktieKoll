using System.Net;
using AktieKoll.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static AktieKoll.Tests.Shared.TestHelpers.ServiceTestHelpers;

namespace AktieKoll.Tests.Unit;

public class FiShortDownloaderTests
{
    [Fact]
    public async Task Download_RetriesThenSucceeds()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError, HttpStatusCode.OK);
        var downloader = new FiShortDownloader(new HttpClient(handler), NullLogger<FiShortDownloader>.Instance)
        {
            BaseDelay = TimeSpan.Zero
        };

        var stream = await downloader.DownloadAsync(FiShortFile.Aggregate, Ct);

        stream.ToArray().Should().Equal(1, 2, 3);
        stream.Position.Should().Be(0);
        handler.Calls.Should().Be(3);
        handler.UserAgent.Should().Be("AktieKoll/1.0 (+https://aktiekoll.com)");
    }

    [Fact]
    public async Task Download_FourFailures_Throws()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError);
        var downloader = new FiShortDownloader(new HttpClient(handler), NullLogger<FiShortDownloader>.Instance)
        {
            BaseDelay = TimeSpan.Zero
        };

        var act = () => downloader.DownloadAsync(FiShortFile.History, Ct);

        await act.Should().ThrowAsync<HttpRequestException>();
        handler.Calls.Should().Be(4);
    }

    private sealed class StubHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? UserAgent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var status = statuses[Math.Min(Calls, statuses.Length - 1)];
            Calls++;
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent([1, 2, 3]) });
        }
    }
}
