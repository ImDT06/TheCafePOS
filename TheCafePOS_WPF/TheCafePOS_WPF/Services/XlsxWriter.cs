using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace TheCafePOS_WPF.Services;

// Minimal .xlsx (Office Open XML) writer: several sheets, bold header row, numbers stay numeric.
// Avoids a third-party dependency for a simple tabular export.
public static class XlsxWriter
{
    public static void Write(string path, IEnumerable<(string Name, IReadOnlyList<string> Header, IEnumerable<object?[]> Rows)> sheets)
    {
        var list = sheets.ToList();
        using var file = File.Create(path);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);
        void Entry(string name, string xml)
        {
            using var w = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
            w.Write(xml);
        }
        var overrides = string.Concat(list.Select((_, i) => $"<Override PartName=\"/xl/worksheets/sheet{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"));
        Entry("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" + overrides + "</Types>");
        Entry("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        Entry("xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>"
            + string.Concat(list.Select((s, i) => $"<sheet name=\"{Esc(SheetName(s.Name))}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>")) + "</sheets></workbook>");
        Entry("xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
            + string.Concat(list.Select((_, i) => $"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>"))
            + $"<Relationship Id=\"rId{list.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
        // Style 1 = bold header, 2 = #,##0 number, 3 = dd/MM/yyyy HH:mm date.
        Entry("xl/styles.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"dd/mm/yyyy hh:mm\"/></numFmts><fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf/></cellStyleXfs><cellXfs count=\"4\"><xf/><xf fontId=\"1\" applyFont=\"1\"/><xf numFmtId=\"3\" applyNumberFormat=\"1\"/><xf numFmtId=\"164\" applyNumberFormat=\"1\"/></cellXfs></styleSheet>");
        for (int i = 0; i < list.Count; i++)
        {
            var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" state=\"frozen\"/></sheetView></sheetViews><cols>");
            for (int c = 0; c < list[i].Header.Count; c++) sb.Append($"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"18\" customWidth=\"1\"/>");
            sb.Append("</cols><sheetData>");
            sb.Append(Row(1, list[i].Header.Cast<object?>().ToArray(), header: true));
            int r = 2;
            foreach (var row in list[i].Rows) sb.Append(Row(r++, row, header: false));
            sb.Append("</sheetData></worksheet>");
            Entry($"xl/worksheets/sheet{i + 1}.xml", sb.ToString());
        }
    }

    private static string Row(int index, object?[] values, bool header)
    {
        var sb = new StringBuilder($"<row r=\"{index}\">");
        for (int c = 0; c < values.Length; c++)
        {
            string cell = Column(c) + index;
            switch (values[c])
            {
                case null: break;
                case decimal or int or long or double when !header:
                    sb.Append($"<c r=\"{cell}\" s=\"2\"><v>{Convert.ToDecimal(values[c]).ToString(CultureInfo.InvariantCulture)}</v></c>"); break;
                case DateTime d when !header:
                    sb.Append($"<c r=\"{cell}\" s=\"3\"><v>{d.ToOADate().ToString(CultureInfo.InvariantCulture)}</v></c>"); break;
                default:
                    sb.Append($"<c r=\"{cell}\" t=\"inlineStr\"{(header ? " s=\"1\"" : "")}><is><t>{Esc(values[c]!.ToString()!)}</t></is></c>"); break;
            }
        }
        return sb.Append("</row>").ToString();
    }

    private static string Column(int i) { string s = ""; for (i++; i > 0; i = (i - 1) / 26) s = (char)('A' + (i - 1) % 26) + s; return s; }
    private static string SheetName(string n) => new string(n.Where(ch => !"[]:*?/\\".Contains(ch)).ToArray()) is { Length: > 31 } s ? s[..31] : n;
    private static string Esc(string s) => SecurityElement.Escape(s) ?? "";
}
