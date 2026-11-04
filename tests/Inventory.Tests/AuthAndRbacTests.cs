namespace Inventory.Tests;

[Collection("api")]
public class AuthAndRbacTests(ApiFixture fx)
{
    [Fact]
    public async Task Login_works_with_email_and_nik_and_never_returns_password()
    {
        var byEmail = await fx.Anonymous().PostAsJsonAsync("/api/auth/login", new { identifier = "admin@inventory.local", password = AdminPassword });
        byEmail.StatusCode.Should().Be(HttpStatusCode.OK);
        (await byEmail.Content.ReadAsStringAsync()).ToLowerInvariant().Should().NotContain("password");
        var byNik = await fx.Anonymous().PostAsJsonAsync("/api/auth/login", new { identifier = "0000000001", password = AdminPassword });
        byNik.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Wrong_password_is_401_and_missing_token_is_401()
    {
        (await fx.Anonymous().PostAsJsonAsync("/api/auth/login", new { identifier = "admin@inventory.local", password = "nope" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await fx.Anonymous().GetAsync("/api/assets")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_token_rotates_and_cannot_be_reused()
    {
        var login = await J(await fx.Anonymous().PostAsJsonAsync("/api/auth/login", new { identifier = "admin@inventory.local", password = AdminPassword }));
        var refresh = login["refreshToken"]!.GetValue<string>();
        (await fx.Anonymous().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await fx.Anonymous().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Member_cannot_delete_or_manage_users_but_can_create()
    {
        var member = await fx.MemberAsync();
        var code = "C" + Guid.NewGuid().ToString("N")[..8];
        var create = await member.PostAsJsonAsync("/api/categories", new { catName = "Tmp", catCode = code, catPrefix = "TM" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await J(create))["catId"]!.GetValue<int>();
        (await member.DeleteAsync($"/api/categories/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await fx.AdminAsync();
        (await admin.DeleteAsync($"/api/categories/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_of_referenced_master_is_rejected_with_409()
    {
        var admin = await fx.AdminAsync();
        var h = await fx.SeedHierarchyAsync(admin);
        (await admin.DeleteAsync($"/api/categories/{h.Cat}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Business_unit_has_multiple_locations_and_asset_status_cannot_be_deleted()
    {
        var admin = await fx.AdminAsync();
        var code = "BU" + Guid.NewGuid().ToString("N")[..8];
        var businessUnit = await J(await admin.PostAsJsonAsync("/api/business-units", new { busUnitCode = code, busUnitPrefix = "BU" }));
        var busUnitId = businessUnit["busUnitId"]!.GetValue<int>();
        var firstLocation = await admin.PostAsJsonAsync("/api/locations", new { busUnitId, locationName = "Location A" });
        var secondLocation = await admin.PostAsJsonAsync("/api/locations", new { busUnitId, locationName = "Location B" });
        firstLocation.StatusCode.Should().Be(HttpStatusCode.Created);
        secondLocation.StatusCode.Should().Be(HttpStatusCode.Created);
        (await J(firstLocation))["busUnitId"]!.GetValue<int>().Should().Be(busUnitId);
        (await J(secondLocation))["busUnitId"]!.GetValue<int>().Should().Be(busUnitId);

        var statuses = await J(await admin.GetAsync("/api/asset-statuses?search=Active"));
        var statusId = statuses["items"]![0]!["statusId"]!.GetValue<int>();
        await fx.CreateAssetAsync(admin, 1);
        (await admin.DeleteAsync($"/api/asset-statuses/{statusId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Hierarchy_mismatch_is_rejected()
    {
        var admin = await fx.AdminAsync();
        var h = await fx.SeedHierarchyAsync(admin);
        var otherCat = await J(await admin.PostAsJsonAsync("/api/categories", new { catName = "Other", catCode = "O" + Guid.NewGuid().ToString("N")[..8], catPrefix = "OT" }));
        var statuses = await J(await admin.GetAsync("/api/asset-statuses?search=Active"));
        var statusId = statuses["items"]![0]!["statusId"]!.GetValue<int>();
        var res = await admin.PostAsJsonAsync("/api/assets", new
        {
            catId = otherCat["catId"]!.GetValue<int>(), groupId = h.Group, typeId = h.Type, brandId = h.Brand, assetName = "x", assetType = "In", statusId, assetPrefix = "H-" + Guid.NewGuid().ToString("N")[..8]
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
