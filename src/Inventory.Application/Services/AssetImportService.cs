using System.Text;
using ClosedXML.Excel;

namespace Inventory.Application.Services;

public class AssetImportService(IAppDbContext db)
{
    public const int MaxRows = 5000;
    private static readonly string[] TemplateHeaders =
    {
        "category_id", "brand_id", "group_id", "type_id", "asset_name", "asset_type", "asset_status", "asset_prefix",
        "category_code", "group_code", "type_code", "brand_code"
    };

    public byte[] BuildTemplate() => ExcelHelper.Build("Assets", TemplateHeaders, new[]
    {
        new object?[] { null, null, null, null, "Lenovo ThinkPad T14", "In", "Active", "LPT-1001", "ELC", "CMP", "LPT", "LNV" }
    });

    public async Task<ImportResult> ImportAsync(Stream content, string fileName, CancellationToken ct)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var rows = ext switch
        {
            ".xlsx" => ReadXlsx(content),
            ".csv" => ReadCsv(content),
            _ => throw new BadRequestException("Only .xlsx and .csv files are accepted.")
        };
        if (rows.Count == 0) throw new BadRequestException("The file contains no data rows.");
        if (rows.Count > MaxRows) throw new BadRequestException($"A maximum of {MaxRows} rows per import is allowed.");

        var cats = await db.Categories.AsNoTracking().ToListAsync(ct);
        var groups = await db.Groups.AsNoTracking().ToListAsync(ct);
        var types = await db.Types.AsNoTracking().ToListAsync(ct);
        var brands = await db.Brands.AsNoTracking().ToListAsync(ct);
        var statuses = await db.AssetStatuses.AsNoTracking().ToListAsync(ct);
        var catById = cats.ToDictionary(x => x.CatId); var catByCode = cats.ToDictionary(x => x.CatCode, StringComparer.OrdinalIgnoreCase);
        var grpById = groups.ToDictionary(x => x.GroupId); var grpByCode = groups.ToDictionary(x => x.GroupCode, StringComparer.OrdinalIgnoreCase);
        var typById = types.ToDictionary(x => x.TypeId); var typByCode = types.ToDictionary(x => x.TypeCode, StringComparer.OrdinalIgnoreCase);
        var brdById = brands.ToDictionary(x => x.BrandId); var brdByCode = brands.ToDictionary(x => x.BrandCode, StringComparer.OrdinalIgnoreCase);

        var prefixes = rows.Select(r => Get(r.Cells, "asset_prefix")).Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existing = new HashSet<string>(
            await db.Assets.AsNoTracking().Where(a => prefixes.Contains(a.AssetPrefix)).Select(a => a.AssetPrefix).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var valid = new List<MdAsset>();
        var errors = new List<ImportError>();
        var now = DateTime.UtcNow;

        foreach (var (rowNumber, c) in rows)
        {
            var errs = new List<string>();
            var cat = Resolve(c, "category_id", "category_code", catById, catByCode, errs, "Category");
            var grp = Resolve(c, "group_id", "group_code", grpById, grpByCode, errs, "Group");
            var typ = Resolve(c, "type_id", "type_code", typById, typByCode, errs, "Type");
            var brd = Resolve(c, "brand_id", "brand_code", brdById, brdByCode, errs, "Brand");

            if (cat is not null && grp is not null && grp.CatId != cat.CatId) errs.Add("Group does not belong to the Category.");
            if (grp is not null && typ is not null && typ.GroupId != grp.GroupId) errs.Add("Type does not belong to the Group.");
            if (cat is not null && typ is not null && typ.CatId != cat.CatId) errs.Add("Type does not belong to the Category.");
            if (typ is not null && brd is not null && brd.TypeId.HasValue && brd.TypeId != typ.TypeId) errs.Add("Brand does not match the Type.");

            var name = Get(c, "asset_name");
            if (name.Length == 0) errs.Add("asset_name is required."); else if (name.Length > 200) errs.Add("asset_name exceeds 200 characters.");
            var statusText = Get(c, "asset_status");
            var status = statuses.FirstOrDefault(s => s.StatusCode.Equals(statusText, StringComparison.OrdinalIgnoreCase)
                || s.StatusName.Equals(statusText, StringComparison.OrdinalIgnoreCase));
            if (statusText.Length == 0) errs.Add("asset_status is required.");
            else if (status is null) errs.Add($"asset_status '{statusText}' does not exist.");

            AssetType type = default;
            var typeText = Get(c, "asset_type");
            if (!Enum.TryParse(typeText, true, out type) || !Enum.IsDefined(type)) errs.Add("asset_type must be 'In' or 'Out'.");

            var prefix = Get(c, "asset_prefix");
            if (prefix.Length == 0) errs.Add("asset_prefix is required.");
            else if (prefix.Length > 50) errs.Add("asset_prefix exceeds 50 characters.");
            else
            {
                if (existing.Contains(prefix)) errs.Add($"asset_prefix '{prefix}' already exists in the database.");
                if (!seen.Add(prefix)) errs.Add($"asset_prefix '{prefix}' is duplicated within the file.");
            }

            if (errs.Count > 0) { errors.Add(new ImportError(rowNumber, errs)); continue; }

            valid.Add(new MdAsset
            {
                CatId = cat!.CatId, GroupId = grp!.GroupId, TypeId = typ!.TypeId, BrandId = brd!.BrandId,
                AssetName = name, AssetType = type, StatusId = status!.StatusId, AssetStatus = status.StatusName, AssetPrefix = prefix,
                Quantity = 0, CreatedDt = now // never trust quantity / audit fields from the file
            });
        }

        if (valid.Count > 0)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Assets.AddRange(valid);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        return new ImportResult(rows.Count, valid.Count, errors.Count, errors.OrderBy(e => e.RowNumber).ToList());
    }

    private static T? Resolve<T>(Dictionary<string, string> c, string idCol, string codeCol, Dictionary<int, T> byId, Dictionary<string, T> byCode,
        List<string> errs, string label) where T : class
    {
        var idText = Get(c, idCol); var code = Get(c, codeCol);
        if (idText.Length > 0)
        {
            if (!int.TryParse(idText, out var id)) { errs.Add($"{idCol} '{idText}' is not a valid number."); return null; }
            if (byId.TryGetValue(id, out var found)) return found;
            errs.Add($"{label} with id {id} does not exist."); return null;
        }
        if (code.Length > 0)
        {
            if (byCode.TryGetValue(code, out var found)) return found;
            errs.Add($"{label} with code '{code}' does not exist."); return null;
        }
        errs.Add($"{label} is required ({idCol} or {codeCol}).");
        return null;
    }

    private static string Get(Dictionary<string, string> c, string key) => c.TryGetValue(key, out var v) ? v.Trim() : string.Empty;

    private static List<(int RowNumber, Dictionary<string, string> Cells)> ReadXlsx(Stream content)
    {
        using var ms = new MemoryStream(); content.CopyTo(ms); ms.Position = 0;
        XLWorkbook wb;
        try { wb = new XLWorkbook(ms); } catch (Exception) { throw new BadRequestException("The file is not a valid .xlsx workbook."); }
        using (wb)
        {
            var ws = wb.Worksheets.First();
            var range = ws.RangeUsed();
            var result = new List<(int, Dictionary<string, string>)>();
            if (range is null) return result;
            int firstRow = range.FirstRow().RowNumber(), lastRow = range.LastRow().RowNumber();
            int firstCol = range.FirstColumn().ColumnNumber(), lastCol = range.LastColumn().ColumnNumber();
            var headers = new List<string>();
            for (var col = firstCol; col <= lastCol; col++) headers.Add(ws.Cell(firstRow, col).GetString().Trim().ToLowerInvariant());
            EnsureHeaders(headers);
            for (var r = firstRow + 1; r <= lastRow; r++)
            {
                var d = new Dictionary<string, string>();
                for (var i = 0; i < headers.Count; i++) if (headers[i].Length > 0) d[headers[i]] = ws.Cell(r, firstCol + i).GetString();
                if (d.Values.All(string.IsNullOrWhiteSpace)) continue;
                result.Add((r, d));
            }
            return result;
        }
    }

    private static List<(int RowNumber, Dictionary<string, string> Cells)> ReadCsv(Stream content)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, true);
        var text = reader.ReadToEnd();
        var lines = ParseCsv(text);
        var result = new List<(int, Dictionary<string, string>)>();
        if (lines.Count == 0) return result;
        var headers = lines[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        EnsureHeaders(headers);
        for (var i = 1; i < lines.Count; i++)
        {
            var d = new Dictionary<string, string>();
            for (var c = 0; c < headers.Count && c < lines[i].Count; c++) if (headers[c].Length > 0) d[headers[c]] = lines[i][c];
            if (d.Values.All(string.IsNullOrWhiteSpace)) continue;
            result.Add((i + 1, d)); // row number as it appears in the file (header = row 1)
        }
        return result;
    }

    private static void EnsureHeaders(List<string> headers)
    {
        var required = new[] { "asset_name", "asset_type", "asset_status", "asset_prefix" };
        var missing = required.Where(h => !headers.Contains(h)).ToList();
        if (missing.Count > 0) throw new BadRequestException($"Missing required column(s): {string.Join(", ", missing)}.");
    }

    private static List<List<string>> ParseCsv(string text)
    {
        if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];
        var firstLineEnd = text.IndexOf('\n');
        var firstLine = firstLineEnd < 0 ? text : text[..firstLineEnd];
        var delim = firstLine.Count(ch => ch == ';') > firstLine.Count(ch => ch == ',') ? ';' : ',';
        var rows = new List<List<string>>(); var row = new List<string>(); var sb = new StringBuilder(); var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; } else inQuotes = false; }
                else sb.Append(ch);
            }
            else if (ch == '"') inQuotes = true;
            else if (ch == delim) { row.Add(sb.ToString()); sb.Clear(); }
            else if (ch == '\r') { }
            else if (ch == '\n') { row.Add(sb.ToString()); sb.Clear(); rows.Add(row); row = new List<string>(); }
            else sb.Append(ch);
        }
        if (sb.Length > 0 || row.Count > 0) { row.Add(sb.ToString()); rows.Add(row); }
        return rows;
    }
}
