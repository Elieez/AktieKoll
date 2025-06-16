using System.Text.RegularExpressions;

namespace AktieKoll.Extensions;

public static class StringExtensions
{
    private static readonly Regex PublRegex = new(@"\s*\(publ\)$", RegexOptions.IgnoreCase);
    private static readonly Regex AbRegex = new(@"\s*\(ab\)$", RegexOptions.IgnoreCase);

    public static string RemovePubl(this string input)
        => string.IsNullOrEmpty(input)
        ? input
        : AbRegex.Replace(PublRegex.Replace(input, ""), "");
}