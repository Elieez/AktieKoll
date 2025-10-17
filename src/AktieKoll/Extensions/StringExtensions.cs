using System.Text.RegularExpressions;

namespace AktieKoll.Extensions;

public static class StringExtensions
{
    private static readonly Regex PublRegex = new(@"\s*\(publ\)", RegexOptions.IgnoreCase);
    private static readonly Regex AbRegex = new(@"\s*\bAB\b", RegexOptions.IgnoreCase);
    private static readonly Regex InternalPrefixRegex = new(@"^Interntransaktion\s*–\s*", RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, string> PositionNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Verkställande direktör (VD)", "VD" },
        { "Annan medlem i bolagets administrations-, lednings- eller kontrollorgan", "Ledningsgrupp" },
        { "Ekonomichef/finanschef/finansdirektör", "CFO" },
        { "Arbetstagarrepresentant i styrelsen eller arbetstagarsuppleant", "Arbetstagarrepresentant" },
    };

    private static readonly (Regex Pattern, string ShortName)[] PositionRegexRules =
    {
        (new Regex(@"\bverkst(ällande)?\s*direktör\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "VD"),
        (new Regex(@"\b(finanschef|ekonomichef|finansdirektör)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "CFO"),
        (new Regex(@"\barbetstag(ar)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Arbetstagarrepresentant"),
    };

    public static string FilterCompanyName(this string input)
        => string.IsNullOrEmpty(input)
        ? input
        : AbRegex.Replace(PublRegex.Replace(input, ""), "");

    public static string FilterTransactionType(this string input)
        => string.IsNullOrWhiteSpace(input)
        ? input
        : InternalPrefixRegex.Replace(input, "");

    public static string FilterPosition(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input ?? string.Empty;

        var s = Regex.Replace(input!, @"\s+", " ").Trim();

        if (PositionNameMap.TryGetValue(s, out var mapped))
            return mapped;

        foreach (var (pattern, shorName) in PositionRegexRules)
        {
            if (pattern.IsMatch(s))
                return shorName;
        }

        return s;
    }
}