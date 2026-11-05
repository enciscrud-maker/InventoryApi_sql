namespace Inventory.Tests;

[Collection("api")]
public class PasswordResetTests(ApiFixture fx)
{
    [Fact]
    public async Task Member_password_reset_requires_matching_details_and_admin_approval()
    {
        var admin = await fx.AdminAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var member = new
        {
            employeeName = $"Reset Member {suffix}",
            employeeNik = $"RNIK{suffix}",
            employeePhone = "555-0199",
            position = "Operator",
            department = "IT",
            businessUnit = "HQ",
            employeeEmail = $"reset-{suffix}@test.local",
            password = "Member#12345",
            role = "Member",
            pagePermissions = new[] { "master-data" },
            isActive = true
        };
        var created = await J(await admin.PostAsJsonAsync("/api/accounts", member));
        var anonymous = fx.Anonymous();
        var requestBody = new
        {
            name = member.employeeName,
            nik = member.employeeNik,
            email = member.employeeEmail,
            phone = member.employeePhone
        };

        (await anonymous.GetAsync("/api/password-reset-requests")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var submitted = await anonymous.PostAsJsonAsync("/api/auth/password-reset", requestBody);
        submitted.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await anonymous.PostAsJsonAsync("/api/auth/password-reset", requestBody))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);

        var requests = await J(await admin.GetAsync("/api/password-reset-requests"));
        var resetRequest = requests["requests"]!.AsArray().SingleOrDefault(item =>
            item?["accountId"]?.GetValue<int>() == created["userId"]!.GetValue<int>())
            ?? throw new InvalidOperationException("The password reset request was not listed.");
        resetRequest["status"]!.GetValue<string>().Should().Be("Pending");
        resetRequest["nik"]!.GetValue<string>().Should().Be(member.employeeNik);
        requests["requests"]!.AsArray().Count(item =>
            item?["accountId"]?.GetValue<int>() == created["userId"]!.GetValue<int>())
            .Should().Be(1);

        using var memberClient = await fx.LoginAsync(member.employeeEmail, member.password);
        (await memberClient.GetAsync("/api/password-reset-requests"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var requestId = resetRequest["id"]!.GetValue<int>();
        var newPassword = "Changed#12345";
        var processed = await admin.PatchAsJsonAsync(
            $"/api/password-reset-requests/{requestId}",
            new { action = "process", newPassword });
        processed.StatusCode.Should().Be(HttpStatusCode.OK, await processed.Content.ReadAsStringAsync());
        (await J(processed))["status"]!.GetValue<string>().Should().Be("Processed");

        using var updatedLogin = await fx.LoginAsync(member.employeeEmail, newPassword);
        (await updatedLogin.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        var oldPassword = await fx.Anonymous().PostAsJsonAsync("/api/auth/login", new
        {
            identifier = member.employeeEmail,
            password = member.password
        });
        oldPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await admin.PatchAsJsonAsync(
            $"/api/password-reset-requests/{requestId}",
            new { action = "process", newPassword = "Another#12345" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Password_reset_submission_is_generic_for_unknown_accounts()
    {
        var response = await fx.Anonymous().PostAsJsonAsync("/api/auth/password-reset", new
        {
            name = "Unknown",
            nik = "UNKNOWN",
            email = "unknown@test.local",
            phone = "555-0100"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await J(response))["message"]!.GetValue<string>()
            .Should().Contain("If the submitted details match");
    }
}
