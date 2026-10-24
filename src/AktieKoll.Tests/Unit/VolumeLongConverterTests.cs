using AktieKoll.Models;

namespace AktieKoll.Tests.Unit;

public class VolumeLongConverterTests
{
    [Theory]
    [InlineData("100", 100L)]
    [InlineData("100,0", 100L)]
    [InlineData("", 0L)]
    // Real row from FI (Episurf warrants, 2026-10-21) that overflowed int and broke the whole import
    [InlineData("5683978525,0", 5_683_978_525L)]
    public void ConvertFromString_ParsesSwedishVolumes(string text, long expected)
    {
        var converter = new VolumeLongConverter();

        var result = converter.ConvertFromString(text, null!, null!);

        Assert.Equal(expected, result);
    }
}
