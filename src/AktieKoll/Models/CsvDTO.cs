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
    public required string PersonNamn { get; set; }
    public required string Befattning { get; set; }
    public string? Närstående { get; set; }
    public string? Korrigering { get; set; }
    [Name("Beskrivning av korrigering")]
    public string? KorrigeringDesc { get; set; }
    [Name("Är förstagångsrapportering")]
    public string? Förstagångsrapportering { get; set; }
    [Name("Är kopplad till aktieprogram")]
    public string? Aktieprogram { get; set; }
    public required string Karaktär { get; set; }
    public required string Instrumenttyp { get; set; }
    public required string Instrumentnamn { get; set; }
    public string? ISIN { get; set; }
    public required DateTime Transaktionsdatum { get; set; }
    [TypeConverter(typeof(VolumeIntConverter))]
    public required int Volym { get; set; }
    public required string Volymsenhet { get; set; }
    public required decimal Pris { get; set; }
    public required string Valuta { get; set; }
    public required string Handelsplats { get; set; }
    public required string Status { get; set; }
}

public class VolumeIntConverter : CsvHelper.TypeConversion.Int32Converter
{
    public override object? ConvertFromString(string? text, CsvHelper.IReaderRow row, CsvHelper.Configuration.MemberMapData memberMapData)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var normalized = text.Replace(',', '.');
        if (decimal.TryParse(normalized, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dec))
            return (int)dec;
        return base.ConvertFromString(text, row, memberMapData);
    }
}