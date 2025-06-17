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
            TransactionType = csvDto.Karaktär,
            Shares = csvDto.Volym,
            Price = csvDto.Pris,
            Currency = csvDto.Valuta,
            Status = csvDto.Status,
            TransactionDate = csvDto.Transaktionsdatum,
            PublishingDate = csvDto.Publiceringsdatum,
        };
    }
    public static class InsiderTradeMapper
    {
        public static List<InsiderTrade> MapDtosToTrades(IEnumerable<CsvDTO> dtos)
            => dtos.Select(dto => dto.ToInsiderTrade()).ToList();
    }
}