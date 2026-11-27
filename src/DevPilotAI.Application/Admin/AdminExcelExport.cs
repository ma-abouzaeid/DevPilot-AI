using ClosedXML.Excel;

namespace DevPilotAI.Application.Admin;

internal static class AdminExcelExport
{
    public static byte[] ToXlsx(
        string sheetName,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<object?>> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Export" : sheetName[..Math.Min(sheetName.Length, 31)]);

        for (var c = 0; c < headers.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
            sheet.Cell(1, c + 1).Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Count; c++)
            {
                var value = row[c];
                var cell = sheet.Cell(r, c + 1);
                switch (value)
                {
                    case null:
                        cell.Value = Blank.Value;
                        break;
                    case DateTime dt:
                        cell.Value = dt;
                        cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                        break;
                    case int i:
                        cell.Value = i;
                        break;
                    case long l:
                        cell.Value = l;
                        break;
                    case decimal d:
                        cell.Value = d;
                        break;
                    case double dbl:
                        cell.Value = dbl;
                        break;
                    case float f:
                        cell.Value = f;
                        break;
                    case bool b:
                        cell.Value = b;
                        break;
                    default:
                        cell.Value = value.ToString();
                        break;
                }
            }

            r++;
        }

        sheet.Columns().AdjustToContents(1, 60);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
