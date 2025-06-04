using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

namespace AktieKoll.Models;

public class CsvDTO
{
    public required DateTime Publiceringsdatum { get; set; }
    public required string Emittent { get; set; }
    [Name("LEI-kod")]
    public required string LEI { get; set; }
    public required string Anmälningsskyldig { get; set; }
    [Name("Person i ledande ställning")]
    public required string PersonNamn{ get; set; }
    public required string Befattning { get; set; }
    public string? Närstående { get; set; } // Could be bool
    public string? Korrigering { get; set; } // Could be bool
    [Name("Beskrivning av korrigering")]
    public string? KorrigeringDesc { get; set; }
    [Name("Är förstagångsrapportering")] 
    public string? Förstagångsrapportering { get; set; } // Could be bool
    [Name("Är kopplad till aktieprogram")]
    public string? Aktieprogram { get; set; }
    public required string Karaktär { get; set; }
    public required string Instrumenttyp { get; set; }
    public required string Instrumentnamn { get; set; }
    public string? ISIN { get; set; }
    public required DateTime Transaktionsdatum { get; set; }
    public required int Volym { get; set; }
    public required string Volymenhet { get; set; }
    public required decimal Pris { get; set; }
    public required string Valuta { get; set; }
    public required string Handelsplats { get; set; }
    public required string Status { get; set; } // Could be bool/enum
}
