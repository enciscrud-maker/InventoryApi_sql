using System.Net.Http.Headers;
using System.Text;
using ClosedXML.Excel;

namespace Inventory.Tests;

[Collection("api")]
public class ImportTests(ApiFixture fx)
{
    private static MultipartFormDataContent File(string name, byte[] bytes)
    {
        var c = new MultipartFormDataContent();
        var f = new ByteArrayContent(bytes); f.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        c.Add(f, "file", name);
        return c;
    }

    [Fact]
    public async Task Csv_import_inserts_valid_rows_and_reports_invalid_ones()
    {
        var admin = await fx.AdminAsync();
        var u = Guid.NewGuid().ToString("N")[..6];
        var csv = new StringBuilder("asset_name,asset_type,asset_status,asset_prefix,category_code,group_code,type_code,brand_code\n");
        csv.AppendLine($"Good one,In,Active,IMP-{u}-1,ELC,CMP,LPT,LNV");        // valid
        csv.AppendLine($"Dup in file,In,Active,IMP-{u}-1,ELC,CMP,LPT,LNV");      // duplicate prefix inside the file
        csv.AppendLine($"Bad type,Sideways,Active,IMP-{u}-2,ELC,CMP,LPT,LNV");   // invalid enum
        csv.AppendLine($"Bad code,In,Active,IMP-{u}-3,NOPE,CMP,LPT,LNV");        // unknown category
        csv.AppendLine("Existing,In,Active,LPT-0001,ELC,CMP,LPT,LNV");           // duplicate in DB

        var res = await admin.PostAsync("/api/assets/import", File("a.csv", Encoding.UTF8.GetBytes(csv.ToString())));
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await J(res);
        body["totalRows"]!.GetValue<int>().Should().Be(5);
        body["successCount"]!.GetValue<int>().Should().Be(1);
        body["failedCount"]!.GetValue<int>().Should().Be(4);
        body["errors"]!.AsArray().Select(e => e!["rowNumber"]!.GetValue<int>()).Should().BeEquivalentTo(new[] { 3, 4, 5, 6 });

        var list = await J(await admin.GetAsync($"/api/assets?search=IMP-{u}"));
        list["totalCount"]!.GetValue<int>().Should().Be(1);
    }

    [Fact]
    public async Task Xlsx_import_resolves_ids_and_rejects_wrong_hierarchy()
    {
        var admin = await fx.AdminAsync();
        var h = await fx.SeedHierarchyAsync(admin);
        var otherCat = await J(await admin.PostAsJsonAsync("/api/categories", new { catName = "X", catCode = "X" + Guid.NewGuid().ToString("N")[..8], catPrefix = "XX" }));
        var u = Guid.NewGuid().ToString("N")[..6];

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Assets");
        var headers = new[] { "category_id", "brand_id", "group_id", "type_id", "asset_name", "asset_type", "asset_status", "asset_prefix" };
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        void Row(int r, int cat, string prefix) { ws.Cell(r, 1).Value = cat; ws.Cell(r, 2).Value = h.Brand; ws.Cell(r, 3).Value = h.Group; ws.Cell(r, 4).Value = h.Type; ws.Cell(r, 5).Value = "XL"; ws.Cell(r, 6).Value = "Out"; ws.Cell(r, 7).Value = "Active"; ws.Cell(r, 8).Value = prefix; }
        Row(2, h.Cat, $"XL-{u}-1");
        Row(3, otherCat["catId"]!.GetValue<int>(), $"XL-{u}-2");
        using var ms = new MemoryStream(); wb.SaveAs(ms);

        var body = await J(await admin.PostAsync("/api/assets/import", File("a.xlsx", ms.ToArray())));
        body["successCount"]!.GetValue<int>().Should().Be(1);
        body["failedCount"]!.GetValue<int>().Should().Be(1);
        body["errors"]![0]!["rowNumber"]!.GetValue<int>().Should().Be(3);
    }

    [Fact]
    public async Task Unsupported_file_type_is_rejected()
    {
        var admin = await fx.AdminAsync();
        (await admin.PostAsync("/api/assets/import", File("a.txt", new byte[] { 1, 2, 3 }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
