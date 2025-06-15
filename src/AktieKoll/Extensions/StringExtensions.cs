using System.Text.RegularExpressions;

namespace AktieKoll.Extensions;

public static class StringExtensions
{
    private static readonly Regex PublRegex = new(@"\s*\(publ\)$", RegexOptions.IgnoreCase);

    public static string RemovePubl(this string input)
        => string.IsNullOrEmpty(input)
        ? input
        : PublRegex.Replace(input, "");
}