using AktieKoll.Models;

namespace AktieKoll.Extensions;

public static class CsvDtoExtensions
{
    public static InsiderTrade ToInsiderTrade(this CsvDTO csvDto)
    {
        return new InsiderTrade
        {
            CompanyName = csvDto.Emittent.FilterCompanyName(),
            InsiderName = csvDto.PersonNamn,
            Position = csvDto.Befattning.FilterPosition(),
            TransactionType = csvDto.Karaktär.FilterTransactionType(),
            Shares = csvDto.Volym,
            Price = csvDto.Pris,
            Currency = csvDto.Valuta,
            Isin = csvDto.ISIN,
            Status = csvDto.Status,
            Lei = csvDto.LEI.NullIfWhiteSpace(),
            InstrumentType = csvDto.Instrumenttyp.NullIfWhiteSpace(),
            TradingVenue = csvDto.Handelsplats.NullIfWhiteSpace(),
            IsCloselyAssociated = csvDto.Närstående.IsFiYes(),
            IsShareProgram = csvDto.Aktieprogram.IsFiYes(),
            ValueSek = string.Equals(csvDto.Valuta, "SEK", StringComparison.OrdinalIgnoreCase)
                ? Math.Round(csvDto.Pris * csvDto.Volym, 2)
                : null,
            TransactionDate = csvDto.Transaktionsdatum,
            PublishingDate = DateTime.SpecifyKind(csvDto.Publiceringsdatum, DateTimeKind.Utc),
        };
    }
    private static string? NullIfWhiteSpace(this string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // FI marks these columns with "Ja" or leaves them empty.
    private static bool IsFiYes(this string? value)
        => string.Equals(value?.Trim(), "Ja", StringComparison.OrdinalIgnoreCase);

    public static class InsiderTradeMapper
    {
        private static readonly HashSet<string> ExcludedTransactionsTypes = new(
            StringComparer.OrdinalIgnoreCase)
        {
            "Lån mottaget",
            "Utdelning lämnad",
            "Utdelning mottagen",
            "Lösen minskning",
            "Lösen ökning",
            "Lån återgång ökning",
            "Lån återgång minskning",
            "Utbyte minskning",
            "Utbyte ökning",
            "Utfärdande av instrument",
            "Pantsättning",
            "Pantsättning åter",
            "Bodelning minskning",
            "Bodelning ökning",
            "Arv mottagen",
            "Konvertering ökning",
            "Lån utlåning",
            "Interntransaktion – Avyttring",
            "Interntransaktion – Förvärv",
            "Inlösen egenutfärdat instrument",
            "Fusion ökning",
            "Gåva lämnad",
            "Gåva mottagen",
            "Koncernintern överföring ökning",
            "Koncernintern överföring minskning",
            "Konvertering minskning"
        };

        private static readonly HashSet<string> ExcludedInstrumentTypes = new(
        StringComparer.OrdinalIgnoreCase)
        {
            "Swap",
            "Ränteswap",
            "BTU",
            "Teckningsrätt/Uniträtt",
            "Kreditderivat",
            "Terminskontrakt",
            "Option"
        };

        public static List<InsiderTrade> MapDtosToTrades(IEnumerable<CsvDTO> dtos)
        => [.. dtos
           .Where(dto => !ExcludedTransactionsTypes.Contains(dto.Karaktär.FilterTransactionType()))
           .Where(dto => !ExcludedInstrumentTypes.Contains(dto.Instrumenttyp))
           .Select(dto => dto.ToInsiderTrade())];
    }
}