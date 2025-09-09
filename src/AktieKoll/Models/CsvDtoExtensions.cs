using AktieKoll.Extensions;

namespace AktieKoll.Models;

public static class CsvDtoExtensions
{
    public static InsiderTrade ToInsiderTrade(this CsvDTO csvDto)
    {
        return new InsiderTrade
        {
            CompanyName = csvDto.Emittent.FilterCompanyName(),
            InsiderName = csvDto.PersonNamn,
            Position = csvDto.Befattning,
            TransactionType = csvDto.Karaktär.FilterTransactionType(),
            Shares = csvDto.Volym,
            Price = csvDto.Pris,
            Currency = csvDto.Valuta,
            Isin = csvDto.ISIN,
            Symbol = csvDto.Symbol,
            Status = csvDto.Status,
            TransactionDate = csvDto.Transaktionsdatum,
            PublishingDate = csvDto.Publiceringsdatum,
        };
    }
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
            "Pantsättning"
        };

        public static List<InsiderTrade> MapDtosToTrades(IEnumerable<CsvDTO> dtos)
            => dtos
            .Where(dto => !ExcludedTransactionsTypes.Contains(dto.Karaktär))
            .Select(dto => dto.ToInsiderTrade())
            .ToList();
    }
}