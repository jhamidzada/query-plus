using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using QueryPlus.Core.Engine;
using QueryPlus.Core.Results;

namespace QueryPlus.App.Services;

/// <summary>Streams a run's merged results into an .xlsx file, row by row (low memory).</summary>
public static class ExcelExporter
{
    // Excel worksheet limit: 1,048,576 rows total (including the header row).
    private const int MaxSheetRows = 1_048_576;

    // Style indices defined in BuildStylesheet: 0 = general, 1 = date, 2 = datetime.
    private const uint DateStyle = 1;
    private const uint DateTimeStyle = 2;

    private static readonly string[] DateFormats =
    {
        "MM/dd/yyyy HH:mm:ss", "M/d/yyyy H:mm:ss",
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss",
        "MM/dd/yyyy", "yyyy-MM-dd"
    };

    /// <summary>Writes the results to <paramref name="path"/>. Returns true if rows were truncated to Excel's limit.</summary>
    public static bool Write(RunReport report, string path)
    {
        var columns = ResultMerger.MergedColumnNames(report);
        var colRefs = new string[columns.Count];
        for (var i = 0; i < columns.Count; i++)
            colRefs[i] = ColumnLetter(i);

        var truncated = false;

        using var doc = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        var workbookPart = doc.AddWorkbookPart();

        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = BuildStylesheet();
        stylesPart.Stylesheet.Save();

        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        using (var writer = OpenXmlWriter.Create(worksheetPart))
        {
            writer.WriteStartElement(new Worksheet());
            writer.WriteStartElement(new SheetData());

            var rowNumber = 1u;
            writer.WriteStartElement(new Row { RowIndex = rowNumber });
            for (var i = 0; i < columns.Count; i++)
                writer.WriteElement(TextCell(colRefs[i] + rowNumber, columns[i]));
            writer.WriteEndElement();

            var maxDataRows = MaxSheetRows - 1;
            var written = 0;
            foreach (var cells in ResultMerger.EnumerateMergedRows(report, columns))
            {
                if (written >= maxDataRows)
                {
                    truncated = true;
                    break;
                }
                rowNumber++;
                writer.WriteStartElement(new Row { RowIndex = rowNumber });
                for (var i = 0; i < cells.Length; i++)
                    writer.WriteElement(BuildCell(colRefs[i] + rowNumber, cells[i]));
                writer.WriteEndElement();
                written++;
            }

            writer.WriteEndElement(); // SheetData
            writer.WriteEndElement(); // Worksheet
        }

        workbookPart.Workbook = new Workbook();
        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = "Results"
        });
        workbookPart.Workbook.Save();

        return truncated;
    }

    /// <summary>Builds a typed cell: number/date where safely detectable, otherwise text.</summary>
    private static Cell BuildCell(string reference, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return new Cell { CellReference = reference };

        if (TryNumber(value))
            return new Cell { CellReference = reference, CellValue = new CellValue(value) };

        if (TryDate(value, out var oaDate, out var style))
            return new Cell { CellReference = reference, StyleIndex = style, CellValue = new CellValue(oaDate) };

        return TextCell(reference, value);
    }

    private static Cell TextCell(string reference, string value) => new()
    {
        CellReference = reference,
        DataType = CellValues.InlineString,
        InlineString = new InlineString(new Text(value) { Space = SpaceProcessingModeValues.Preserve })
    };

    /// <summary>
    /// True only for a clean numeric literal safe to store as a number: optional sign, no leading
    /// zeros (so IDs like "007" stay text), no more than 15 significant digits (double precision).
    /// </summary>
    private static bool TryNumber(string v)
    {
        var n = v.Length;
        var i = v[0] == '-' ? 1 : 0;
        if (i >= n)
            return false;

        var dotAt = -1;
        var digits = 0;
        for (var j = i; j < n; j++)
        {
            var c = v[j];
            if (c == '.')
            {
                if (dotAt >= 0)
                    return false;
                dotAt = j;
            }
            else if (c is >= '0' and <= '9')
            {
                digits++;
            }
            else
            {
                return false;
            }
        }

        if (digits == 0 || digits > 15)
            return false;

        var intEnd = dotAt >= 0 ? dotAt : n;
        var intLen = intEnd - i;
        if (intLen == 0)
            return false;                       // ".5" — keep as text
        if (intLen > 1 && v[i] == '0')
            return false;                       // "007" — keep as text
        return true;
    }

    private static bool TryDate(string v, out string oaDate, out uint style)
    {
        oaDate = string.Empty;
        style = DateStyle;
        if (!DateTime.TryParseExact(v, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt))
            return false;
        if (dt.Year < 1900)                     // Excel's 1900 date system can't represent earlier
            return false;

        style = dt.TimeOfDay == TimeSpan.Zero ? DateStyle : DateTimeStyle;
        oaDate = dt.ToOADate().ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static Stylesheet BuildStylesheet() => new(
        new NumberingFormats(
            new NumberingFormat { NumberFormatId = 164U, FormatCode = "yyyy\\-mm\\-dd" },
            new NumberingFormat { NumberFormatId = 165U, FormatCode = "yyyy\\-mm\\-dd\\ hh:mm:ss" }
        ) { Count = 2U },
        new Fonts(new Font()) { Count = 1U },
        new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 })
        ) { Count = 2U },
        new Borders(new Border()) { Count = 1U },
        new CellStyleFormats(new CellFormat()) { Count = 1U },
        new CellFormats(
            new CellFormat(),                                                          // 0 general
            new CellFormat { NumberFormatId = 164U, ApplyNumberFormat = true },        // 1 date
            new CellFormat { NumberFormatId = 165U, ApplyNumberFormat = true }         // 2 datetime
        ) { Count = 3U });

    /// <summary>0 → A, 25 → Z, 26 → AA, …</summary>
    private static string ColumnLetter(int index)
    {
        var letters = string.Empty;
        index++;
        while (index > 0)
        {
            var rem = (index - 1) % 26;
            letters = (char)('A' + rem) + letters;
            index = (index - 1) / 26;
        }
        return letters;
    }
}
