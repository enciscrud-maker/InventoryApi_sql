using ClosedXML.Excel;

namespace Inventory.Application.Services;

public static class ExcelHelper
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Build(string sheetName, string[] headers, IEnumerable<object?[]> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(sheetName);
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
        }
        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length; c++)
            {
                var v = row[c];
                if (v is null) continue;
                ws.Cell(r, c + 1).Value = v switch
                {
                    string s => s, int i => i, long l => l, decimal d => d, double db => db, bool b => b,
                    DateTime dt => dt, Enum e => e.ToString(), _ => v.ToString() ?? string.Empty
                };
            }
            r++;
        }
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
