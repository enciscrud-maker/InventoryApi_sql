namespace Inventory.Tests;

[Collection("api")]
public class AuthAndRbacTests(ApiFixture fx)
{
    [Fact]
    public async Task Dashboard_summary_returns_category_aggregates()
    {
        var admin = await fx.AdminAsync();

        var response = await admin.GetAsync("/api/dashboard/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var summary = await J(response);
        summary["totalAssets"]!.GetValue<int>().Should().BeGreaterThan(0);
        summary["assetsByCategory"]!.AsArray().Should().NotBeEmpty();
        summary["assetsByCategory"]!.AsArray()
            .Select(item => item!["category"]!.GetValue<string>())
            .Should().BeInAscendingOrder();
    }

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
        var member = await fx.MemberAsync("master-data");
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
    public async Task Main_admin_is_distinguished_and_only_it_can_manage_accounts()
    {
        var mainAdmin = await fx.AdminAsync();
        var profile = await J(await mainAdmin.GetAsync("/api/auth/me"));
        profile["isMainAdmin"]!.GetValue<bool>().Should().BeTrue();
        profile["employeeNik"]!.GetValue<string>().Should().Be("0000000001");
        profile["pagePermissions"]!.AsArray().Select(x => x!.GetValue<string>())
            .Should().Contain(new[] { "dashboard", "asset-inventory", "goods-in", "goods-out", "stock-opname", "master-data", "reports", "settings" });
        (await mainAdmin.GetAsync("/api/goods-in")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await mainAdmin.GetAsync("/api/goods-out")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await mainAdmin.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.OK);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var created = await J(await mainAdmin.PostAsJsonAsync("/api/accounts", new
        {
            employeeName = "Scoped Admin " + suffix,
            employeeNik = "ANIK" + suffix,
            employeeEmail = $"a{suffix}@test.local",
            position = "Analyst",
            department = "IT",
            businessUnit = "HQ",
            password = "Scoped#12345",
            role = "Admin",
            pagePermissions = new[] { "asset-inventory" },
            isActive = true
        }));
        created["pagePermissions"]!.AsArray().Select(x => x!.GetValue<string>())
            .Should().Equal("asset-inventory");

        var scopedAdmin = await fx.LoginAsync($"a{suffix}@test.local", "Scoped#12345");
        var scopedProfile = await J(await scopedAdmin.GetAsync("/api/auth/me"));
        scopedProfile["isMainAdmin"]!.GetValue<bool>().Should().BeFalse();
        (await scopedAdmin.GetAsync("/api/assets")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await scopedAdmin.GetAsync("/api/goods-in")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await scopedAdmin.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await scopedAdmin.PostAsJsonAsync("/api/categories", new
        {
            catName = "Denied",
            catCode = "D" + suffix,
            catPrefix = "DN"
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Designated_nik_keeps_main_admin_access_if_database_flag_is_not_set()
    {
        await fx.SetDesignatedMainAdminFlagAsync(false);
        try
        {
            var admin = await fx.LoginAsync("0000000001", AdminPassword);
            var profile = await J(await admin.GetAsync("/api/auth/me"));
            profile["isMainAdmin"]!.GetValue<bool>().Should().BeTrue();
            (await admin.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await admin.GetAsync("/api/goods-in")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await admin.GetAsync("/api/goods-out")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await fx.SetDesignatedMainAdminFlagAsync(true);
        }
    }

    [Fact]
    public async Task Existing_access_token_without_main_admin_claim_receives_database_admin_authorization()
    {
        var admin = await fx.LoginWithoutMainAdminClaimAsync("0000000001");
        var profile = await J(await admin.GetAsync("/api/auth/me"));
        profile["isMainAdmin"]!.GetValue<bool>().Should().BeTrue();
        (await admin.GetAsync("/api/assets")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync("/api/goods-in")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync("/api/goods-out")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Member_reads_all_pages_but_can_create_only_on_assigned_pages()
    {
        var member = await fx.MemberAsync("asset-inventory");
        (await member.GetAsync("/api/categories")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await member.PostAsJsonAsync("/api/categories", new
        {
            catName = "Denied",
            catCode = "D" + Guid.NewGuid().ToString("N")[..8],
            catPrefix = "DN"
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var assetId = await fx.CreateAssetAsync(member, 2);
        (await member.PostAsJsonAsync("/api/goods-in", ApiFixture.GoodBody(TransactionType.Return, assetId, 1)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task User_permissions_can_be_read_and_replaced_as_an_exact_allowlist()
    {
        var member = await fx.MemberAsync("master-data");
        var profile = await J(await member.GetAsync("/api/auth/me"));
        var userId = profile["userId"]!.GetValue<int>();
        var admin = await fx.AdminAsync();

        (await member.GetAsync($"/api/users/{userId}/permissions")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var catalog = await J(await admin.GetAsync("/api/permissions"));
        var assetCreateId = catalog.AsArray().Single(p => p!["permissionName"]!.GetValue<string>() == Permissions.AssetCreate)!["permissionId"]!.GetValue<int>();
        var update = await admin.PostAsJsonAsync($"/api/users/{userId}/permissions", new { permissionIds = new[] { assetCreateId } });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var saved = await J(update);
        var allowed = saved["permissions"]!.AsArray()
            .Where(p => p!["isAllowed"]!.GetValue<bool>())
            .Select(p => p!["permissionName"]!.GetValue<string>());
        allowed.Should().BeEquivalentTo(Permissions.MemberDefaults.Append(Permissions.AssetCreate));
        saved["permissions"]!.AsArray().Single(p => p!["permissionId"]!.GetValue<int>() == assetCreateId)!["isDirectlyAssigned"]!.GetValue<bool>().Should().BeTrue();

        var user = await J(await admin.GetAsync($"/api/users/{userId}"));
        user["roles"]!.AsArray().Select(role => role!.GetValue<string>()).Should().Contain(RoleNames.Member);

        var freshMember = await fx.LoginAsync(profile["employeeEmail"]!.GetValue<string>(), "Member#12345");
        (await freshMember.GetAsync("/api/assets")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await freshMember.GetAsync("/api/categories")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await freshMember.PostAsJsonAsync("/api/categories", new { catName = "Denied", catCode = "D" + Guid.NewGuid().ToString("N")[..8], catPrefix = "DN" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task User_cannot_remove_their_own_user_management_permission()
    {
        var admin = await fx.AdminAsync();
        var profile = await J(await admin.GetAsync("/api/auth/me"));
        var response = await admin.PostAsJsonAsync($"/api/users/{profile["userId"]!.GetValue<int>()}/permissions", new { permissionIds = Array.Empty<int>() });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
        var invalidLocation = await admin.PostAsJsonAsync("/api/locations", new { busUnitId = 0, locationName = "string" });
        invalidLocation.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var code = "BU" + Guid.NewGuid().ToString("N")[..8];
        var businessUnit = await J(await admin.PostAsJsonAsync("/api/business-units", new { busUnitCode = code, busUnitPrefix = "BU" }));
        var busUnitId = businessUnit["busUnitId"]!.GetValue<int>();
        var firstLocation = await admin.PostAsJsonAsync("/api/locations", new { busUnitId, locationName = "Location A" });
        var secondLocation = await admin.PostAsJsonAsync("/api/locations", new { busUnitId, locationName = "Location B" });
        firstLocation.StatusCode.Should().Be(HttpStatusCode.Created);
        secondLocation.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstLocationDto = await J(firstLocation);
        var secondLocationDto = await J(secondLocation);
        firstLocationDto["busUnitId"]!.GetValue<int>().Should().Be(busUnitId);
        secondLocationDto["busUnitId"]!.GetValue<int>().Should().Be(busUnitId);
        firstLocationDto["locationCode"]!.GetValue<string>().Should().StartWith($"LOC-{code}-");
        secondLocationDto["locationCode"]!.GetValue<string>().Should().StartWith($"LOC-{code}-")
            .And.NotBe(firstLocationDto["locationCode"]!.GetValue<string>());
        firstLocationDto["isActive"]!.GetValue<bool>().Should().BeTrue();

        var statuses = await J(await admin.GetAsync("/api/asset-statuses?search=Active"));
        var statusId = statuses["items"]![0]!["statusId"]!.GetValue<int>();
        var assetId = await fx.CreateAssetAsync(admin, 1);
        var asset = await J(await admin.GetAsync($"/api/assets/{assetId}"));
        asset["assetStatus"]!.GetValue<string>().Should().Be(statuses["items"]![0]!["statusName"]!.GetValue<string>());
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
        var assetPrefix = "H-" + Guid.NewGuid().ToString("N")[..8];
        var res = await admin.PostAsJsonAsync("/api/assets", new
        {
            catId = otherCat["catId"]!.GetValue<int>(), groupId = h.Group, typeId = h.Type, brandId = h.Brand, assetName = "x", assetType = "In", statusId,
            assetPrefix, assetCode = assetPrefix + "00001"
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
