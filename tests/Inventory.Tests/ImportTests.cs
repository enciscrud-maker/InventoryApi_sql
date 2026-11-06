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
    public async Task Asset_prefix_can_be_shared_and_asset_code_is_required_and_unique()
    {
        var admin = await fx.AdminAsync();
        var hierarchy = await fx.SeedHierarchyAsync(admin);
        var statuses = await J(await admin.GetAsync("/api/asset-statuses?search=Active"));
        var statusId = statuses["items"]![0]!["statusId"]!.GetValue<int>();
        var body = new
        {
            catId = hierarchy.Cat,
            groupId = hierarchy.Group,
            typeId = hierarchy.Type,
            brandId = hierarchy.Brand,
            assetName = "Code test",
            assetType = "In",
            statusId,
            assetPrefix = "QCVASDW",
            assetCode = "QCVASDW00001"
        };

        var first = await admin.PostAsJsonAsync("/api/assets", body);
        first.StatusCode.Should().Be(HttpStatusCode.Created, await first.Content.ReadAsStringAsync());
        var created = await J(first);
        created["assetPrefix"]!.GetValue<string>().Should().Be("QCVASDW");
        created["assetCode"]!.GetValue<string>().Should().Be("QCVASDW00001");

        var samePrefix = await admin.PostAsJsonAsync("/api/assets", new
        {
            body.catId, body.groupId, body.typeId, body.brandId, assetName = body.assetName, body.assetType,
            body.statusId, body.assetPrefix, assetCode = "QCVASDW00002"
        });
        samePrefix.StatusCode.Should().Be(HttpStatusCode.Created, await samePrefix.Content.ReadAsStringAsync());

        (await admin.PostAsJsonAsync("/api/assets", body)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync("/api/assets", new
        {
            body.catId, body.groupId, body.typeId, body.brandId, assetName = body.assetName, body.assetType,
            body.statusId, body.assetPrefix, assetCode = "NOT-A-MATCH"
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Csv_import_inserts_valid_rows_and_reports_invalid_ones()
    {
        var admin = await fx.AdminAsync();
        var u = Guid.NewGuid().ToString("N")[..6];
        var csv = new StringBuilder("asset_name,asset_type,asset_status,asset_prefix,asset_code,category_code,group_code,type_code,brand_code\n");
        csv.AppendLine($"Good one,In,Active,IMP-{u},IMP-{u}00001,ELC,CMP,LPT,LNV");              // valid
        csv.AppendLine($"Good two,In,Active,IMP-{u},IMP-{u}00002,ELC,CMP,LPT,LNV");              // shared prefix, unique code
        csv.AppendLine($"Dup in file,In,Active,IMP-{u},IMP-{u}00001,ELC,CMP,LPT,LNV");           // duplicate code in file
        csv.AppendLine($"Bad type,Sideways,Active,IMP-{u},IMP-{u}00003,ELC,CMP,LPT,LNV");         // invalid enum
        csv.AppendLine($"Bad code,In,Active,IMP-{u},IMP-{u}00004,NOPE,CMP,LPT,LNV");              // unknown category
        csv.AppendLine("Existing,In,Active,LPT,LPT00001,ELC,CMP,LPT,LNV");                       // duplicate code in DB

        var res = await admin.PostAsync("/api/assets/import", File("a.csv", Encoding.UTF8.GetBytes(csv.ToString())));
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await J(res);
        body["totalRows"]!.GetValue<int>().Should().Be(6);
        body["successCount"]!.GetValue<int>().Should().Be(2);
        body["failedCount"]!.GetValue<int>().Should().Be(4);
        body["errors"]!.AsArray().Select(e => e!["rowNumber"]!.GetValue<int>()).Should().BeEquivalentTo(new[] { 4, 5, 6, 7 });

        var list = await J(await admin.GetAsync($"/api/assets?search=IMP-{u}"));
        list["totalCount"]!.GetValue<int>().Should().Be(2);
        list["items"]!.AsArray().Select(item => item!["assetCode"]!.GetValue<string>())
            .Should().BeEquivalentTo(new[] { $"IMP-{u}00001", $"IMP-{u}00002" });
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
        var headers = new[] { "category_id", "brand_id", "group_id", "type_id", "asset_name", "asset_type", "asset_status", "asset_prefix", "asset_code" };
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        void Row(int r, int cat, string prefix, string code) { ws.Cell(r, 1).Value = cat; ws.Cell(r, 2).Value = h.Brand; ws.Cell(r, 3).Value = h.Group; ws.Cell(r, 4).Value = h.Type; ws.Cell(r, 5).Value = "XL"; ws.Cell(r, 6).Value = "Out"; ws.Cell(r, 7).Value = "Active"; ws.Cell(r, 8).Value = prefix; ws.Cell(r, 9).Value = code; }
        Row(2, h.Cat, $"XL-{u}", $"XL-{u}00001");
        Row(3, otherCat["catId"]!.GetValue<int>(), $"XL-{u}", $"XL-{u}00002");
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
