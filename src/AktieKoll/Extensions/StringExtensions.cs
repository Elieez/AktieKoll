using System.Text.RegularExpressions;

namespace AktieKoll.Extensions;

public static partial class StringExtensions
{
    [GeneratedRegex(@"\s*\(publ\)", RegexOptions.IgnoreCase)]
    private static partial Regex PublRegex();

    [GeneratedRegex(@"\s*\bAB\b", RegexOptions.IgnoreCase)]
    private static partial Regex AbRegex();

    [GeneratedRegex(@"^Interntransaktion\s*–\s*", RegexOptions.IgnoreCase)]
    private static partial Regex InternalPrefixRegex();

    [GeneratedRegex(@"\s+", RegexOptions.None)]
    private static partial Regex WhitespaceRegex();

    // NEW: Regex for trailing punctuation
    [GeneratedRegex(@"[.,;]+$", RegexOptions.None)]
    private static partial Regex TrailingPunctuationRegex();

    // NEW: Regex for " Series A/B" suffix
    [GeneratedRegex(@"\s+Series\s+[AB]$", RegexOptions.IgnoreCase)]
    private static partial Regex SeriesRegex();

    private static readonly Dictionary<string, string> PositionNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Verkställande direktör (VD)", "VD" },
        { "Annan medlem i bolagets administrations-, lednings- eller kontrollorgan", "Ledningsgrupp" },
        { "Ekonomichef/finanschef/finansdirektör", "CFO" },
        { "Arbetstagarrepresentant i styrelsen eller arbetstagarsuppleant", "Arbetstagarrepresentant" },
    };

    private static readonly (Regex Pattern, string ShortName)[] PositionRegexRules =
    [
        (VerkstAllandeRegex(), "VD"),
        (FinansChefRegex(), "CFO"),
        (ArbetstagRegex(), "Arbetstagarrepresentant"),
    ];

    [GeneratedRegex(@"\bverkst(ällande)?\s*direktör\b", RegexOptions.IgnoreCase)]
    private static partial Regex VerkstAllandeRegex();

    [GeneratedRegex(@"\b(finanschef|ekonomichef|finansdirektör)\b", RegexOptions.IgnoreCase)]
    private static partial Regex FinansChefRegex();

    [GeneratedRegex(@"\barbetstag(ar)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex ArbetstagRegex();

    public static string FilterCompanyName(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input ?? string.Empty;

        var result = input;

        // Remove (publ)
        result = PublRegex().Replace(result, "");

        // Remove AB
        result = AbRegex().Replace(result, "");

        // Remove " Series A/B"
        result = SeriesRegex().Replace(result, "");

        // Remove trailing punctuation (., ; ,)
        result = TrailingPunctuationRegex().Replace(result, "");

        // Normalize whitespace (multiple spaces to single space)
        result = WhitespaceRegex().Replace(result, " ").Trim();

        return result;
    }

    public static string FilterTransactionType(this string input)
        => string.IsNullOrWhiteSpace(input)
        ? input
        : InternalPrefixRegex().Replace(input, "");

    public static string FilterPosition(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input ?? string.Empty;

        var s = WhitespaceRegex().Replace(input!, " ").Trim();

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