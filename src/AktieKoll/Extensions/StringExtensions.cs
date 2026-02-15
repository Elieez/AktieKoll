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

    [GeneratedRegex(@"\s+", RegexOptions.None)]
    private static partial Regex WhitespaceRegex();

    public static string FilterCompanyName(this string input)
        => string.IsNullOrEmpty(input)
        ? input
        : AbRegex().Replace(PublRegex().Replace(input, ""), "");

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