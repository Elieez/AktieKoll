using AktieKoll.Models;

namespace AktieKoll.Tests.Extensions;

public static class FakeDTO
{
    public static CsvDTO MakeCsvDto(Action<CsvDTO>? customize = null)
    {
        var dto = new CsvDTO
        {
            Publiceringsdatum = DateTime.Today,
            Emittent = "FooCorp",
            LEI = string.Empty,
            Anmälningsskyldig = string.Empty,
            PersonNamn = "Alice",
            Befattning = "CFO",
            Karaktär = "Förvärv",
            Instrumenttyp = string.Empty,
            Instrumentnamn = string.Empty,
            Transaktionsdatum = DateTime.Today,
            Volym = 100,
            Volymsenhet = string.Empty,
            Pris = 10.5m,
            Valuta = "SEK",
            Handelsplats = string.Empty,
            ISIN = "SE0001",
            Status = "Aktuell"
        };

        customize?.Invoke(dto);
        return dto;
    }
}
