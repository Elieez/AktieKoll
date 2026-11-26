using AktieKoll.Dtos;
using AktieKoll.Models;
using AktieKoll.Services;
using FluentAssertions;

namespace AktieKoll.Tests.Unit;

public class ShortTrendTests
{
    private static readonly DateTime Today = new(2026, 11, 26);

    [Fact]
    public void FullWindow_ReturnsDiffAndNullSince()
    {
        var pts = new[] { new SeriesPointDto(new DateTime(2026, 10, 26), 1.0m), new SeriesPointDto(Today, 2.2m) };
        var (change, since) = ShortTrend.ComputeChange(pts, Today, 30);
        change.Should().Be(1.2m);
        since.Should().BeNull();
    }

    [Fact]
    public void ShortWindow_UsesOldestAndReturnsSince()
    {
        var pts = new[] { new SeriesPointDto(new DateTime(2026, 11, 20), 1.0m), new SeriesPointDto(Today, 0.6m) };
        var (change, since) = ShortTrend.ComputeChange(pts, Today, 30);
        change.Should().Be(-0.4m);
        since.Should().Be(new DateTime(2026, 11, 20));
    }

    [Fact]
    public void SinglePoint_ReturnsNulls()
    {
        var (change, since) = ShortTrend.ComputeChange([new SeriesPointDto(Today, 1m)], Today, 30);
        change.Should().BeNull();
        since.Should().BeNull();
    }

    [Fact]
    public void SignificantSeries_StepsAndDropsBelowThreshold()
    {
        ShortPosition P(string h, decimal pct, bool below, int day) => new()
        {
            HolderName = h, IssuerName = "X", Isin = "SE0000000001", PositionPercent = pct,
            BelowThreshold = below, PositionDate = new DateTime(2027 - 1, 1, day),
        };
        var today = new DateTime(2026, 1, 20);
        var events = new[] { P("A", 0.6m, false, 1), P("B", 0.7m, false, 5), P("A", 0.5m, true, 9) };

        var series = ShortTrend.SignificantSeries(events, today);

        series.Select(s => (s.Date, s.Value)).Should().Equal(
            (new DateTime(2026, 1, 1), 0.6m),
            (new DateTime(2026, 1, 5), 1.3m),
            (new DateTime(2026, 1, 9), 0.7m),
            (today, 0.7m));
    }
}
