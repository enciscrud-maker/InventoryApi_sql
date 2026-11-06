using System.Net;

namespace Inventory.Tests;

[Collection("api")]
public class AssetTests(ApiFixture fx)
{
    [Fact]
    public async Task Create_and_update_asset_persist_location_purchase_order_and_sap_material()
    {
        var admin = await fx.AdminAsync();
        var hierarchy = await fx.SeedHierarchyAsync(admin);
        var statuses = await ApiFixture.J(await admin.GetAsync("/api/asset-statuses?search=Active"));
        var status = statuses["items"]!.AsArray()[0]!;
        var statusId = status["statusId"]!.GetValue<int>();
        var statusName = status["statusName"]!.GetValue<string>();
        var units = await ApiFixture.J(await admin.GetAsync("/api/business-units"));
        var unit = units["items"]!.AsArray()[0]!;
        var businessUnitId = unit["busUnitId"]!.GetValue<int>();
        var businessUnit = unit["busUnitCode"]!.GetValue<string>();
        var locations = await ApiFixture.J(await admin.GetAsync($"/api/locations?busUnitId={businessUnitId}&pageSize=100"));
        var location = locations["items"]!.AsArray()
            .FirstOrDefault(item => item!["busUnitId"]!.GetValue<int>() == businessUnitId);
        var locationId = location?["locationId"]?.GetValue<int>();
        var prefix = "AST-" + Guid.NewGuid().ToString("N")[..8];
        var createdDt = DateTime.UtcNow.AddYears(-10);
        var createBody = new
        {
            assetId = 1007,
            catId = hierarchy.Cat,
            catName = "ignored client category",
            groupId = hierarchy.Group,
            groupName = "ignored client group",
            typeId = hierarchy.Type,
            typeName = "ignored client type",
            brandId = hierarchy.Brand,
            brandName = "ignored client brand",
            assetName = "Asset persistence test",
            assetType = "In",
            statusId,
            assetStatus = statusName,
            assetPrefix = prefix,
            createdDt,
            quantity = 1,
            unitOfMeasure = "UNIT",
            serialNumber = prefix + "-SERIAL",
            assetTag = prefix + "-TAG",
            purchaseOrderNumber = prefix + "-PO",
            sapMaterialNumber = prefix + "-SAP",
            assetValue = 20000000m,
            description = "Asset persistence test",
            busUnitId = businessUnitId,
            conditionId = (int?)null,
            conditionName = (string?)null,
            businessUnit,
            rowVersion = (string?)null,
            assetCode = prefix + "-00001",
            locationId,
            locationName = location?["locationName"]?.GetValue<string>(),
            modelId = (int?)null,
            modelName = (string?)null
        };

        var createdResponse = await admin.PostAsJsonAsync("/api/assets", createBody);
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created, await createdResponse.Content.ReadAsStringAsync());
        var created = await ApiFixture.J(createdResponse);
        var assetId = created["assetId"]!.GetValue<int>();
        assetId.Should().BePositive();
        assetId.Should().NotBe(1007);
        created["createdDt"]!.GetValue<DateTime>().Should().BeAfter(createdDt);
        created["locationId"]?.GetValue<int>().Should().Be(locationId);
        created["purchaseOrderNumber"]!.GetValue<string>().Should().Be(prefix + "-PO");
        created["sapMaterialNumber"]!.GetValue<string>().Should().Be(prefix + "-SAP");

        var updateBody = new
        {
            createBody.catId,
            createBody.groupId,
            createBody.typeId,
            createBody.brandId,
            createBody.assetName,
            createBody.assetType,
            createBody.statusId,
            createBody.assetPrefix,
            createBody.assetCode,
            createBody.quantity,
            createBody.unitOfMeasure,
            createBody.serialNumber,
            createBody.assetTag,
            purchaseOrderNumber = prefix + "-PO-UPDATED",
            sapMaterialNumber = prefix + "-SAP-UPDATED",
            createBody.assetValue,
            createBody.description,
            createBody.busUnitId,
            createBody.conditionId,
            createBody.businessUnit,
            rowVersion = created["rowVersion"]!.GetValue<string>(),
            locationId = (int?)null,
            createBody.modelId
        };
        var updatedResponse = await admin.PutAsJsonAsync($"/api/assets/{assetId}", updateBody);
        updatedResponse.StatusCode.Should().Be(HttpStatusCode.OK, await updatedResponse.Content.ReadAsStringAsync());
        var updated = await ApiFixture.J(updatedResponse);
        updated["locationId"].Should().BeNull();
        updated["purchaseOrderNumber"]!.GetValue<string>().Should().Be(prefix + "-PO-UPDATED");
        updated["sapMaterialNumber"]!.GetValue<string>().Should().Be(prefix + "-SAP-UPDATED");

        var nextAssetId = await fx.CreateAssetAsync(admin, 1, businessUnitId: businessUnitId);
        nextAssetId.Should().BeGreaterThan(assetId);
    }
}
