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
    public async Task Hierarchy_mismatch_is_rejected()
    {
        var admin = await fx.AdminAsync();
        var h = await fx.SeedHierarchyAsync(admin);
        var otherCat = await J(await admin.PostAsJsonAsync("/api/categories", new { catName = "Other", catCode = "O" + Guid.NewGuid().ToString("N")[..8], catPrefix = "OT" }));
        var res = await admin.PostAsJsonAsync("/api/assets", new
        {
            catId = otherCat["catId"]!.GetValue<int>(), groupId = h.Group, typeId = h.Type, brandId = h.Brand, assetName = "x", assetType = "In", assetStatus = "Active", assetPrefix = "H-" + Guid.NewGuid().ToString("N")[..8]
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
