namespace Inventory.Tests;

[Collection("api")]
public class GoodsTests(ApiFixture fx)
{
    [Fact]
    public async Task Goods_in_increases_and_goods_out_decreases_stock_and_total_assets_is_sum_of_quantity()
    {
        var m = await fx.MemberAsync();
        var asset = await fx.CreateAssetAsync(m, 10);

        var inRes = await m.PostAsJsonAsync("/api/goods-in", GoodBody(TransactionType.Return, asset, 5));
        inRes.StatusCode.Should().Be(HttpStatusCode.Created, await inRes.Content.ReadAsStringAsync());
        (await fx.QuantityAsync(m, asset)).Should().Be(15);

        var outRes = await m.PostAsJsonAsync("/api/goods-out", GoodBody(TransactionType.Borrowing, asset, 4));
        outRes.StatusCode.Should().Be(HttpStatusCode.Created);
        (await fx.QuantityAsync(m, asset)).Should().Be(11);

        var list = await J(await m.GetAsync("/api/goods-out?search=Borrowing"));
        list["items"]!.AsArray().Any(i => i!["totalAssets"]!.GetValue<int>() == 4 && i["documentNumber"]!.GetValue<string>().StartsWith("GO-")).Should().BeTrue();
        (await J(inRes))["status"]!.GetValue<string>().Should().Be("InProgress");
    }

    [Fact]
    public async Task Goods_out_cannot_exceed_available_stock()
    {
        var m = await fx.MemberAsync();
        var asset = await fx.CreateAssetAsync(m, 3);
        var res = await m.PostAsJsonAsync("/api/goods-out", GoodBody(TransactionType.Handover, asset, 4));
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await fx.QuantityAsync(m, asset)).Should().Be(3);
    }

    [Fact]
    public async Task Wrong_transaction_type_for_direction_is_rejected()
    {
        var m = await fx.MemberAsync();
        var asset = await fx.CreateAssetAsync(m, 3);
        (await m.PostAsJsonAsync("/api/goods-in", GoodBody(TransactionType.Borrowing, asset, 1))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Disposal_sets_condition_to_disposal()
    {
        var m = await fx.MemberAsync();
        var asset = await fx.CreateAssetAsync(m, 2);
        (await m.PostAsJsonAsync("/api/goods-out", GoodBody(TransactionType.Disposal, asset, 1))).StatusCode.Should().Be(HttpStatusCode.Created);
        var a = await J(await m.GetAsync($"/api/assets/{asset}"));
        a["conditionName"]!.GetValue<string>().Should().Be("Disposal");
    }

    [Fact]
    public async Task Duplicate_request_id_is_rejected_and_stock_is_applied_once()
    {
        var m = await fx.MemberAsync();
        var asset = await fx.CreateAssetAsync(m, 10);
        var body = GoodBody(TransactionType.Service, asset, 2, requestId: "dup-" + Guid.NewGuid());
        (await m.PostAsJsonAsync("/api/goods-out", body)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await m.PostAsJsonAsync("/api/goods-out", body)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await fx.QuantityAsync(m, asset)).Should().Be(8);
    }

    [Fact]
    public async Task Concurrent_issues_cannot_over_issue_stock()
    {
        var m = await fx.MemberAsync();
        var asset = await fx.CreateAssetAsync(m, 10);
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => m.PostAsJsonAsync("/api/goods-out", GoodBody(TransactionType.Borrowing, asset, 7))));
        results.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        results.Count(r => r.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest).Should().Be(1);
        (await fx.QuantityAsync(m, asset)).Should().Be(3);
    }

    [Fact]
    public async Task Status_change_writes_one_history_row_without_reapplying_stock()
    {
        var m = await fx.AdminAsync();
        var asset = await fx.CreateAssetAsync(m, 10);
        var created = await J(await m.PostAsJsonAsync("/api/goods-out", GoodBody(TransactionType.Borrowing, asset, 3)));
        var id = created["goodId"]!.GetValue<int>();

        var same = await J(await m.PatchAsJsonAsync($"/api/goods-out/{id}/status", new { status = "InProgress" }));
        same["statusHistory"]!.AsArray().Count.Should().Be(0);

        var done = await J(await m.PatchAsJsonAsync($"/api/goods-out/{id}/status", new { status = "Completed", notes = "returned docs" }));
        var hist = done["statusHistory"]!.AsArray();
        hist.Count.Should().Be(1);
        hist[0]!["previousStatus"]!.GetValue<string>().Should().Be("InProgress");
        hist[0]!["newStatus"]!.GetValue<string>().Should().Be("Completed");
        hist[0]!["actorNik"]!.GetValue<string>().Should().Be("0000000001");
        (await fx.QuantityAsync(m, asset)).Should().Be(7);

        (await m.PatchAsJsonAsync($"/api/goods-out/{id}/status", new { status = "Bogus" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await m.GetAsync($"/api/goods-in/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound); // wrong direction
    }
}
