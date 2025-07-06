using System.Text.RegularExpressions;

namespace AktieKoll.Extensions;

public static class StringExtensions
{
    private static readonly Regex PublRegex = new(@"\s*\(publ\)", RegexOptions.IgnoreCase);
    private static readonly Regex AbRegex = new(@"\s*\bAB\b", RegexOptions.IgnoreCase);
    private static readonly Regex InternalPrefixRegex = new(@"^Interntransaktion\s*–\s*", RegexOptions.IgnoreCase);

    public static string FilterCompanyName(this string input)
        => string.IsNullOrEmpty(input)
        ? input
        : AbRegex.Replace(PublRegex.Replace(input, ""), "");

    public static string FilterTransactionType(this string input)
        => string.IsNullOrWhiteSpace(input)
        ? input
        : InternalPrefixRegex.Replace(input, "");
}