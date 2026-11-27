using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace AktieKoll.Services;

/// <summary>Minimal ODS reader: zip + content.xml, first table only.</summary>
public static class OdsReader
{
    private static readonly XNamespace TableNs = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace TextNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    // Guard against pathological repeat counts on cells/rows that actually hold data.
    private const int MaxRepeat = 10_000;

    public static List<List<string>> ReadFirstSheet(Stream ods)
    {
        using var zip = new ZipArchive(ods, ZipArchiveMode.Read, leaveOpen: true);
        var entry = zip.GetEntry("content.xml")
            ?? throw new InvalidDataException("ODS file has no content.xml");

        XDocument doc;
        using (var s = entry.Open())
            doc = XDocument.Load(s);

        var table = doc.Descendants(TableNs + "table").FirstOrDefault()
            ?? throw new InvalidDataException("ODS file has no table");

        var rows = new List<List<string>>();
        foreach (var rowEl in table.Descendants(TableNs + "table-row"))
        {
            var cells = ReadRow(rowEl);
            if (cells.Count == 0)
                continue; // empty row (incl. huge number-rows-repeated blocks)

            int repeat = Math.Clamp(RepeatCount(rowEl, "number-rows-repeated"), 1, MaxRepeat);
            for (int i = 0; i < repeat; i++)
                rows.Add(i == 0 ? cells : [.. cells]);
        }
        return rows;
    }

    private static List<string> ReadRow(XElement rowEl)
    {
        var cells = new List<string>();
        int pendingEmpty = 0;
        foreach (var cellEl in rowEl.Elements().Where(e => e.Name == TableNs + "table-cell" || e.Name == TableNs + "covered-table-cell"))
        {
            int repeat = Math.Clamp(RepeatCount(cellEl, "number-columns-repeated"), 1, MaxRepeat);
            var text = CellText(cellEl);
            if (text.Length == 0)
            {
                pendingEmpty += repeat; // only materialised if a non-empty cell follows
                continue;
            }
            for (int i = 0; i < pendingEmpty; i++) cells.Add("");
            pendingEmpty = 0;
            for (int i = 0; i < repeat; i++) cells.Add(text);
        }
        return cells;
    }

    private static int RepeatCount(XElement el, string attr)
        => int.TryParse((string?)el.Attribute(TableNs + attr), out var n) && n > 0 ? n : 1;

    private static string CellText(XElement cell)
    {
        var paragraphs = cell.Elements(TextNs + "p").Select(ParagraphText).ToList();
        return string.Join(" ", paragraphs);
    }

    private static string ParagraphText(XElement p)
    {
        var sb = new StringBuilder();
        Append(p, sb);
        return sb.ToString();
    }

    private static void Append(XElement el, StringBuilder sb)
    {
        foreach (var node in el.Nodes())
        {
            switch (node)
            {
                case XText t:
                    sb.Append(t.Value);
                    break;
                case XElement e when e.Name == TextNs + "s":
                    int c = int.TryParse((string?)e.Attribute(TextNs + "c"), out var n) && n > 0 ? n : 1;
                    sb.Append(' ', c);
                    break;
                case XElement e when e.Name == TextNs + "tab":
                    sb.Append('\t');
                    break;
                case XElement e:
                    Append(e, sb);
                    break;
            }
        }
    }
}
