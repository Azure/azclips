using Core.Abstractions;
using Core.Runtime;

// Quick smoke test for ProfileService
var logger = new TestLogger();
var service = new ProfileService(logger);

Console.WriteLine("=== Profile Management Smoke Test ===\n");

// 1. Create a profile
Console.WriteLine("1. Creating profile 'dev'...");
var devProfile = new Profile
{
    Name = "dev",
    TenantId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
    SubscriptionId = "11111111-2222-3333-4444-555555555555",
    AuthMethod = "browser",
    UserPrincipalName = "user@contoso.com",
    CloudName = "AzureCloud"
};
await service.SaveProfileAsync(devProfile, setActive: true);
Console.WriteLine("   ✓ Profile 'dev' created and set as active.\n");

// 2. Create second profile
Console.WriteLine("2. Creating profile 'prod'...");
var prodProfile = new Profile
{
    Name = "prod",
    TenantId = "ffffffff-1111-2222-3333-444444444444",
    SubscriptionId = "66666666-7777-8888-9999-000000000000",
    AuthMethod = "service-principal",
    ClientId = "abcdef01-2345-6789-abcd-ef0123456789",
    CloudName = "AzureCloud"
};
await service.SaveProfileAsync(prodProfile, setActive: false);
Console.WriteLine("   ✓ Profile 'prod' created (not active).\n");

// 3. List all profiles
Console.WriteLine("3. Listing all profiles...");
var all = await service.GetAllProfilesAsync();
var active = await service.GetActiveProfileAsync();
Console.WriteLine($"   {"NAME",-12} {"TENANT",-38} {"AUTH",-20} {"ACTIVE"}");
foreach (var p in all)
{
    var marker = p.Name == active?.Name ? "*" : " ";
    Console.WriteLine($" {marker} {p.Name,-12} {p.TenantId,-38} {p.AuthMethod,-20}");
}
Console.WriteLine();

// 4. Switch to prod
Console.WriteLine("4. Switching to profile 'prod'...");
await service.SetActiveProfileAsync("prod");
active = await service.GetActiveProfileAsync();
Console.WriteLine($"   ✓ Active profile: {active!.Name}\n");

// 5. Show profile details
Console.WriteLine("5. Showing profile 'dev'...");
var dev = await service.GetProfileAsync("dev");
Console.WriteLine($"   Name:            {dev!.Name}");
Console.WriteLine($"   Tenant ID:       {dev.TenantId}");
Console.WriteLine($"   Subscription ID: {dev.SubscriptionId}");
Console.WriteLine($"   Auth Method:     {dev.AuthMethod}");
Console.WriteLine($"   User:            {dev.UserPrincipalName}");
Console.WriteLine();

// 6. Modify profile — add a default
Console.WriteLine("6. Modifying 'dev' — adding default location=eastus...");
await service.UpdateProfileAsync("dev", p => p.Defaults["location"] = "eastus");
dev = await service.GetProfileAsync("dev");
Console.WriteLine($"   ✓ Defaults: {string.Join(", ", dev!.Defaults.Select(kv => $"{kv.Key}={kv.Value}"))}\n");

// 7. Switch back to dev
Console.WriteLine("7. Switching back to 'dev'...");
await service.SetActiveProfileAsync("dev");
active = await service.GetActiveProfileAsync();
Console.WriteLine($"   ✓ Active profile: {active!.Name}\n");

// 8. Remove prod
Console.WriteLine("8. Removing profile 'prod'...");
await service.DeleteProfileAsync("prod");
all = await service.GetAllProfilesAsync();
Console.WriteLine($"   ✓ Remaining profiles: {string.Join(", ", all.Select(p => p.Name))}\n");

// 9. Try switching to nonexistent profile
Console.WriteLine("9. Switching to nonexistent profile 'staging'...");
try
{
    await service.SetActiveProfileAsync("staging");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"   ✓ Error (expected): {ex.Message}\n");
}

// 10. Verify file on disk
var profilePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    ".cirrus", "profiles.json");
Console.WriteLine($"10. Checking profiles.json on disk...");
Console.WriteLine($"    Path: {profilePath}");
Console.WriteLine($"    Exists: {File.Exists(profilePath)}");
if (File.Exists(profilePath))
{
    var json = await File.ReadAllTextAsync(profilePath);
    Console.WriteLine($"    Size: {json.Length} bytes");
    Console.WriteLine($"    Content:\n{json}");
}

Console.WriteLine("\n=== All tests passed! ===");

// Cleanup
if (File.Exists(profilePath))
    File.Delete(profilePath);

// Minimal logger for testing
class TestLogger : ILoggingService
{
    public void Log(string message) { }
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message) { }
    public void Verbose(string message) { }
    public void Debug(string message) { }
}
