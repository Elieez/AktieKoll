using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace AktieKoll.Tests.Shared.TestHelpers;

/// <summary>
/// Builds a minimal ODS zip (mimetype + content.xml) mimicking FI's structure, including
/// the huge trailing empty column/row repeats LibreOffice emits.
/// </summary>
public static class OdsTestBuilder
{
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    public static MemoryStream Build(IEnumerable<string[]> rows)
    {
        var tableEl = new XElement(Table + "table", new XAttribute(Table + "name", "Blad1"));
        foreach (var row in rows)
        {
            var rowEl = new XElement(Table + "table-row");
            foreach (var cell in row)
                rowEl.Add(BuildCell(cell));
            rowEl.Add(new XElement(Table + "table-cell", new XAttribute(Table + "number-columns-repeated", "1000")));
            tableEl.Add(rowEl);
        }
        tableEl.Add(new XElement(Table + "table-row",
            new XAttribute(Table + "number-rows-repeated", "1000"),
            new XElement(Table + "table-cell", new XAttribute(Table + "number-columns-repeated", "1000"))));

        var doc = new XDocument(
            new XElement(Office + "document-content",
                new XAttribute(XNamespace.Xmlns + "office", Office),
                new XAttribute(XNamespace.Xmlns + "table", Table),
                new XAttribute(XNamespace.Xmlns + "text", Text),
                new XElement(Office + "body",
                    new XElement(Office + "spreadsheet", tableEl))));

        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var mime = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var w = new StreamWriter(mime.Open(), new UTF8Encoding(false)))
                w.Write("application/vnd.oasis.opendocument.spreadsheet");

            var content = zip.CreateEntry("content.xml");
            using var cs = content.Open();
            doc.Save(cs);
        }
        ms.Position = 0;
        return ms;
    }

    private static XElement BuildCell(string value)
    {
        if (value.Length == 0)
            return new XElement(Table + "table-cell");

        var p = new XElement(Text + "p");
        // Leading spaces are written as text:s like LibreOffice does.
        int lead = 0;
        while (lead < value.Length && value[lead] == ' ') lead++;
        if (lead > 0)
            p.Add(new XElement(Text + "s", new XAttribute(Text + "c", lead)));
        p.Add(new XText(value[lead..]));

        return new XElement(Table + "table-cell",
            new XAttribute(Office + "value-type", "string"),
            p);
    }
}
