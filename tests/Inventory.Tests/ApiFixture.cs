using Microsoft.AspNetCore.Hosting;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MsSql;

namespace Inventory.Tests;

/// <summary>Spins up a real SQL Server container (Docker required) and the API on top of it.</summary>
public class ApiFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder().Build();
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public const string AdminPassword = "Admin#12345";

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _sql.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Key", "integration-test-key-that-is-at-least-32-chars!!");
        Environment.SetEnvironmentVariable("Seed__SampleData", "true");
        Environment.SetEnvironmentVariable("Seed__AdminPassword", AdminPassword);
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development"));
        _ = Factory.CreateClient(); // forces startup + migrations + seeding
    }

    public async Task DisposeAsync() { Factory.Dispose(); await _sql.DisposeAsync(); }

    public HttpClient Anonymous() => Factory.CreateClient();

    public async Task<HttpClient> LoginAsync(string identifier, string password)
    {
        var c = Factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/login", new { identifier, password });
        res.EnsureSuccessStatusCode();
        var json = (await res.Content.ReadFromJsonAsync<JsonNode>())!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json["accessToken"]!.GetValue<string>());
        return c;
    }

    public Task<HttpClient> AdminAsync() => LoginAsync("admin@inventory.local", AdminPassword);

    /// <summary>Creates an employee + Member user through the admin API and returns a logged-in client.</summary>
    public async Task<HttpClient> MemberAsync()
    {
        var admin = await AdminAsync();
        var n = Guid.NewGuid().ToString("N")[..8];
        var emp = await (await admin.PostAsJsonAsync("/api/employees", new { employeeName = "Member " + n, employeeNik = "NIK" + n, department = "Ops" })).Content.ReadFromJsonAsync<JsonNode>();
        var res = await admin.PostAsJsonAsync("/api/users", new { employeeId = emp!["employeeId"]!.GetValue<int>(), employeeEmail = $"m{n}@test.local", password = "Member#12345" });
        res.EnsureSuccessStatusCode();
        return await LoginAsync($"m{n}@test.local", "Member#12345");
    }

    // ---- helpers ----
    public static async Task<JsonNode> J(HttpResponseMessage r) => (await r.Content.ReadFromJsonAsync<JsonNode>())!;

    public async Task<(int Cat, int Group, int Type, int Brand)> SeedHierarchyAsync(HttpClient admin)
    {
        var page = await J(await admin.GetAsync("/api/brands?search=Lenovo"));
        var brand = page["items"]![0]!;
        var type = await J(await admin.GetAsync($"/api/asset-types/{brand["typeId"]!.GetValue<int>()}"));
        return (type["catId"]!.GetValue<int>(), type["groupId"]!.GetValue<int>(), type["typeId"]!.GetValue<int>(), brand["brandId"]!.GetValue<int>());
    }

    public async Task<int> CreateAssetAsync(HttpClient c, int quantity, string? prefix = null)
    {
        var h = await SeedHierarchyAsync(await AdminAsync());
        var statuses = await J(await c.GetAsync("/api/asset-statuses?search=Active"));
        var statusId = statuses["items"]![0]!["statusId"]!.GetValue<int>();
        var res = await c.PostAsJsonAsync("/api/assets", new
        {
            catId = h.Cat, groupId = h.Group, typeId = h.Type, brandId = h.Brand, assetName = "Test asset", assetType = "In", statusId,
            assetPrefix = prefix ?? "T-" + Guid.NewGuid().ToString("N")[..10], quantity
        });
        res.StatusCode.Should().Be(System.Net.HttpStatusCode.Created, await res.Content.ReadAsStringAsync());
        return (await J(res))["assetId"]!.GetValue<int>();
    }

    public static object GoodBody(TransactionType type, int assetId, int qty, string? requestId = null) => new
    {
        requestId = requestId ?? Guid.NewGuid().ToString(), goodName = "Test " + type, transactionType = type.ToString(), businessUnit = "Corporate",
        requestDate = DateTime.UtcNow, formIssueDate = DateTime.UtcNow, personName = "Budi", personNik = "123", personDepartment = "IT",
        destinationBusinessUnit = "HQ", approverName = "Siti",
        items = new[] { new { assetId, quantity = qty, requestJustification = "needed", requiredDuration = "1 month", intendedPurpose = "work" } }
    };

    public async Task<int> QuantityAsync(HttpClient c, int assetId) => (await J(await c.GetAsync($"/api/assets/{assetId}")))["quantity"]!.GetValue<int>();
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFixture> { }
