using System.Net;
using Inventory.Application.Abstractions;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Tests;

[Collection("api")]
public class AuditTests(ApiFixture fx)
{
    [Fact]
    public async Task Create_stock_audit_persists_all_assets_and_authenticated_auditor_atomically()
    {
        var admin = await fx.AdminAsync();
        var units = await ApiFixture.J(await admin.GetAsync("/api/business-units"));
        var unit = units["items"]!.AsArray()[0]!;
        var businessUnitId = unit["busUnitId"]!.GetValue<int>();
        var businessUnit = unit["busUnitCode"]!.GetValue<string>();
        var firstAssetId = await fx.CreateAssetAsync(admin, 4, businessUnitId: businessUnitId);
        var secondAssetId = await fx.CreateAssetAsync(admin, 7, businessUnitId: businessUnitId);

        var response = await admin.PostAsJsonAsync("/api/stock-audits", new
        {
            stockOpnameName = "Multi asset audit",
            stockOpnameType = "In",
            assetId = firstAssetId,
            assetIds = new[] { firstAssetId, secondAssetId },
            auditDate = DateTime.UtcNow.Date,
            businessUnit,
            busUnitId = businessUnitId,
            stockOpnameStatus = "InProgress"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var audit = await ApiFixture.J(response);
        audit["auditorUserId"]!.GetValue<int>().Should().BeGreaterThan(0);
        var items = audit["items"]!.AsArray();
        items.Should().HaveCount(2);
        items.Select(item => item!["assetId"]!.GetValue<int>())
            .Should().BeEquivalentTo(new[] { firstAssetId, secondAssetId });
        items.All(item =>
                item!["finding"]!.GetValue<string>() == "Unverified" &&
                item["physicalQuantity"] is null)
            .Should().BeTrue();

        var detailResponse = await admin.GetAsync($"/api/stock-audits/{audit["stockOpnameId"]}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await ApiFixture.J(detailResponse);
        detail["items"]!.AsArray().Select(item => item!["assetId"]!.GetValue<int>())
            .Should().BeEquivalentTo(new[] { firstAssetId, secondAssetId });

        var listResponse = await admin.GetAsync("/api/stock-audits?page=1&pageSize=100");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auditList = await ApiFixture.J(listResponse);
        var listedAudit = auditList["items"]!.AsArray()
            .Single(item => item!["stockOpnameId"]!.GetValue<int>() == audit["stockOpnameId"]!.GetValue<int>());
        listedAudit!["itemCount"]!.GetValue<int>().Should().Be(2);

        var duplicateResponse = await admin.PostAsJsonAsync("/api/stock-audits", new
        {
            stockOpnameName = "Duplicate asset audit",
            stockOpnameType = "In",
            assetId = firstAssetId,
            assetIds = new[] { firstAssetId, firstAssetId },
            auditDate = DateTime.UtcNow.Date,
            businessUnit,
            busUnitId = businessUnitId,
            stockOpnameStatus = "InProgress"
        });
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Audit_list_marks_verified_only_when_all_assets_match_and_are_confirmed()
    {
        var admin = await fx.AdminAsync();
        var units = await ApiFixture.J(await admin.GetAsync("/api/business-units"));
        var unit = units["items"]!.AsArray()[0]!;
        var businessUnitId = unit["busUnitId"]!.GetValue<int>();
        var businessUnit = unit["busUnitCode"]!.GetValue<string>();
        var assetId = await fx.CreateAssetAsync(admin, 4, businessUnitId: businessUnitId);
        var audit = await CreateAuditAsync(admin, businessUnit, businessUnitId, assetId);
        var auditId = audit["stockOpnameId"]!.GetValue<int>();
        var item = audit["items"]!.AsArray().Single()!;
        var detailId = item["stockOpnameDetailId"]!.GetValue<int>();
        var systemQuantity = item["systemQuantity"]!.GetValue<int>();

        async Task<bool> IsVerifiedAsync()
        {
            var response = await admin.GetAsync("/api/stock-audits?page=1&pageSize=100");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await ApiFixture.J(response);
            return result["items"]!.AsArray()
                .Single(row => row!["stockOpnameId"]!.GetValue<int>() == auditId)!
                ["isVerified"]!.GetValue<bool>();
        }

        (await IsVerifiedAsync()).Should().BeFalse();

        async Task UpdateFindingAsync(string finding, int physicalQuantity, bool confirmed = true)
        {
            var response = await admin.PutAsJsonAsync($"/api/stock-audits/{auditId}/items", new
            {
                items = new[]
                {
                    new
                    {
                        stockOpnameDetailId = detailId,
                        assetId,
                        physicalQuantity,
                        actualLocationId = (int?)null,
                        finding,
                        isConfirmed = confirmed,
                        notes = (string?)null
                    }
                }
            });
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }

        await UpdateFindingAsync("Fisik ada data berbeda", systemQuantity + 1);
        (await IsVerifiedAsync()).Should().BeFalse();

        await UpdateFindingAsync("Match", systemQuantity, confirmed: false);
        (await IsVerifiedAsync()).Should().BeFalse();

        await UpdateFindingAsync("Match", systemQuantity);
        (await IsVerifiedAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_stock_audit_removes_only_session_details_and_requires_delete_permission()
    {
        var admin = await fx.AdminAsync();
        var units = await ApiFixture.J(await admin.GetAsync("/api/business-units"));
        var unit = units["items"]!.AsArray()[0]!;
        var businessUnitId = unit["busUnitId"]!.GetValue<int>();
        var businessUnit = unit["busUnitCode"]!.GetValue<string>();
        var firstAssetId = await fx.CreateAssetAsync(admin, 4, businessUnitId: businessUnitId);
        var secondAssetId = await fx.CreateAssetAsync(admin, 7, businessUnitId: businessUnitId);

        var firstAudit = await CreateAuditAsync(admin, businessUnit, businessUnitId, firstAssetId, secondAssetId);
        var secondAudit = await CreateAuditAsync(admin, businessUnit, businessUnitId, firstAssetId);
        var firstAuditId = firstAudit["stockOpnameId"]!.GetValue<int>();
        var secondAuditId = secondAudit["stockOpnameId"]!.GetValue<int>();

        var member = await fx.MemberAsync("stock-opname");
        var forbidden = await member.DeleteAsync($"/api/stock-audits/{firstAuditId}");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.GetAsync($"/api/stock-audits/{firstAuditId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var deleted = await admin.DeleteAsync($"/api/stock-audits/{firstAuditId}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetAsync($"/api/stock-audits/{firstAuditId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var remaining = await ApiFixture.J(await admin.GetAsync($"/api/stock-audits/{secondAuditId}"));
        remaining["items"]!.AsArray().Should().ContainSingle()
            .Which!["assetId"]!.GetValue<int>().Should().Be(firstAssetId);
        (await admin.GetAsync($"/api/assets/{firstAssetId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/assets/{secondAssetId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Startup_repair_restores_missing_root_audit_detail_without_duplicates()
    {
        var admin = await fx.AdminAsync();
        var units = await ApiFixture.J(await admin.GetAsync("/api/business-units"));
        var unit = units["items"]!.AsArray()[0]!;
        var businessUnitId = unit["busUnitId"]!.GetValue<int>();
        var businessUnit = unit["busUnitCode"]!.GetValue<string>();
        var firstAssetId = await fx.CreateAssetAsync(admin, 4, businessUnitId: businessUnitId);
        var secondAssetId = await fx.CreateAssetAsync(admin, 7, businessUnitId: businessUnitId);
        var audit = await CreateAuditAsync(admin, businessUnit, businessUnitId, firstAssetId, secondAssetId);
        var auditId = audit["stockOpnameId"]!.GetValue<int>();

        await using var scope = fx.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var oldDetails = await db.StockOpnameDetails.Where(detail => detail.StockOpnameId == auditId).ToListAsync();
        db.StockOpnameDetails.RemoveRange(oldDetails);
        await db.SaveChangesAsync();

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        await DbSeeder.InitializeAsync(db, hasher, configuration);
        await DbSeeder.InitializeAsync(db, hasher, configuration);

        var restored = await ApiFixture.J(await admin.GetAsync($"/api/stock-audits/{auditId}"));
        restored["items"]!.AsArray().Should().ContainSingle()
            .Which!["assetId"]!.GetValue<int>().Should().Be(firstAssetId);
    }

    private static async Task<System.Text.Json.Nodes.JsonNode> CreateAuditAsync(
        HttpClient admin,
        string businessUnit,
        int businessUnitId,
        int firstAssetId,
        params int[] additionalAssetIds)
    {
        var assetIds = new[] { firstAssetId }.Concat(additionalAssetIds).ToArray();
        var response = await admin.PostAsJsonAsync("/api/stock-audits", new
        {
            stockOpnameName = "Delete test audit",
            stockOpnameType = "In",
            assetId = firstAssetId,
            assetIds,
            auditDate = DateTime.UtcNow.Date,
            businessUnit,
            busUnitId = businessUnitId,
            stockOpnameStatus = "InProgress"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await ApiFixture.J(response);
    }
}
