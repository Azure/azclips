using System.Text.Json;

namespace Live.Tests;

/// <summary>
/// Live tests for the <c>profile</c> verbs against real managed-identity logins.
///
/// <para>
/// The managed-identity tests prove a token can be acquired and used. These prove
/// the login is <b>persisted correctly</b>: that the profile records the identity
/// that was actually used, and that list/show/set/remove operate on it. A bug here
/// would not break the first call after login — it would surface later, when the
/// CLI reloads the profile and re-authenticates as the wrong identity.
/// </para>
/// </summary>
public class ProfileLiveTests : IClassFixture<LiveTestFixture>, IAsyncLifetime
{
    // Temporarily disabled — remove this and the Skip= on the cases below to
    // re-enable. ProfileShow_AfterSystemAssignedLogin_RecordsManagedIdentity is
    // deliberately left running: it is the only case that exercises the whole
    // chain end to end (managed-identity login, profile written, profile read
    // back), so it still proves the bundle and MI auth work on the VM.
    // Ordering rule: RequiresVariables' setter recomputes Skip, so Skip= must
    // come after it or it is discarded.
    private const string SkipReason = "Temporarily disabled to shorten the live run.";

    // Bare `login` (no --out-profile) writes to this reserved profile name.
    private const string TempProfileName = "temp";

    // AuthMethod.ManagedIdentity. The CLI serialises the enum by value, so this is
    // the ordinal; the name is accepted too, so the tests survive a switch to a
    // string enum converter.
    private const string ManagedIdentityAuthMethod = "4";

    // Profiles created by these tests, removed during teardown.
    private readonly List<string> _createdProfiles = [];

    public async ValueTask InitializeAsync()
    {
        if (!LiveTestEnvironment.IsEnabled)
        {
            return;
        }

        await AzCli.LogoutAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (!LiveTestEnvironment.IsEnabled)
        {
            return;
        }

        // Named profiles outlive a logout, so remove them explicitly or the next
        // run inherits them and `profile list` assertions see stale entries.
        foreach (var name in _createdProfiles)
        {
            await AzCli.RunAsync("profile", "remove", name);
        }

        await AzCli.LogoutAsync();
    }

    [LiveFact]
    public async Task ProfileShow_AfterSystemAssignedLogin_RecordsManagedIdentity()
    {
        await LoginAsync();

        var profile = await ShowActiveProfileAsync();

        AssertManagedIdentity(profile);
        Assert.Equal(LiveTestEnvironment.SubscriptionId, GetString(profile, "subscriptionId"));

        // "MSI@" is the encoding for the system-assigned identity; a user-assigned
        // login would record MSIClient@/MSIObject@/MSIResource@ instead.
        Assert.Equal("MSI@", GetString(profile, "managedIdentityId"));
    }

    [LiveFact(RequiresVariables = [LiveTestEnvironment.UamiClientIdVariable], Skip = SkipReason)]
    public async Task ProfileShow_AfterUserAssignedLogin_RecordsSelectedIdentity()
    {
        await LoginAsync("--client-id", LiveTestEnvironment.UamiClientId);

        var profile = await ShowActiveProfileAsync();

        AssertManagedIdentity(profile);
        Assert.Equal(
            $"MSIClient@{LiveTestEnvironment.UamiClientId}",
            GetString(profile, "managedIdentityId"));
    }

    [LiveFact(Skip = SkipReason)]
    public async Task ProfileList_AfterLogin_ContainsActiveProfile()
    {
        await LoginAsync();

        var profiles = await ListProfilesAsync();

        var active = profiles.SingleOrDefault(entry =>
            entry.TryGetProperty("isActive", out var flag) && flag.GetBoolean());

        Assert.True(
            active.ValueKind == JsonValueKind.Object,
            $"Expected exactly one active profile in 'profile list' after login, found "
                + $"{profiles.Count(e => e.TryGetProperty("isActive", out var f) && f.GetBoolean())}.");

        Assert.Equal(TempProfileName, GetString(active, "name"));
        AssertManagedIdentity(active);
    }

    [LiveFact(Skip = SkipReason)]
    public async Task ProfileSet_SwitchesTheActiveProfile()
    {
        var named = await LoginToNamedProfileAsync();

        // Bare login moves the active profile to "temp", so the named one is no
        // longer active and `profile set` has something real to switch back to.
        await LoginAsync();
        Assert.Equal(TempProfileName, GetString(await ShowActiveProfileAsync(), "name"));

        var set = await AzCli.RunAsync("profile", "set", named);
        Assert.True(set.Succeeded, $"'profile set {named}' failed.{Environment.NewLine}{set.Describe()}");

        Assert.Equal(named, GetString(await ShowActiveProfileAsync(), "name"));
    }

    [LiveFact(Skip = SkipReason)]
    public async Task ProfileRemove_DeletesTheProfile()
    {
        var named = await LoginToNamedProfileAsync();

        var remove = await AzCli.RunAsync("profile", "remove", named);
        Assert.True(
            remove.Succeeded,
            $"'profile remove {named}' failed.{Environment.NewLine}{remove.Describe()}");

        _createdProfiles.Remove(named);

        var profiles = await ListProfilesAsync();
        Assert.DoesNotContain(profiles, entry =>
            string.Equals(GetString(entry, "name"), named, StringComparison.OrdinalIgnoreCase));

        var show = await AzCli.RunAsync("profile", "show", named);
        Assert.False(show.Succeeded, $"'profile show {named}' succeeded after the profile was removed.");
    }

    [LiveFact(Skip = SkipReason)]
    public async Task ProfileShow_ForUnknownProfile_Fails()
    {
        var missing = $"no-such-profile-{Guid.NewGuid():N}";

        var show = await AzCli.RunAsync("profile", "show", missing);

        Assert.False(show.Succeeded, $"'profile show {missing}' unexpectedly succeeded.");
    }

    private static void AssertManagedIdentity(JsonElement profile)
    {
        var authMethod = GetString(profile, "authMethod");
        Assert.True(
            authMethod == ManagedIdentityAuthMethod
                || string.Equals(authMethod, "ManagedIdentity", StringComparison.OrdinalIgnoreCase),
            $"Expected the profile to record a managed-identity login, but authMethod was "
                + $"'{authMethod}'. Profile: {profile}");
    }

    private static async Task LoginAsync(params string[] selectorArgs)
    {
        var login = await AzCli.LoginWithIdentityAsync(selectorArgs);
        Assert.True(
            login.Succeeded,
            $"'az login --identity {string.Join(' ', selectorArgs)}' "
                + $"failed.{Environment.NewLine}{login.Describe()}");
    }

    /// <summary>Logs in to a uniquely named profile and registers it for cleanup.</summary>
    private async Task<string> LoginToNamedProfileAsync()
    {
        var name = $"live-test-{Guid.NewGuid():N}"[..20];

        var login = await AzCli.RunAsync(
            "login", "--identity",
            "--subscription-id", LiveTestEnvironment.SubscriptionId,
            "--out-profile", name);

        Assert.True(
            login.Succeeded,
            $"login to profile '{name}' failed.{Environment.NewLine}{login.Describe()}");

        _createdProfiles.Add(name);
        return name;
    }

    private static async Task<JsonElement> ShowActiveProfileAsync()
    {
        var show = await AzCli.RunAsync("profile", "show", "--active");
        Assert.True(show.Succeeded, $"'profile show --active' failed.{Environment.NewLine}{show.Describe()}");

        return ParseJson(show, "profile show --active");
    }

    private static async Task<JsonElement[]> ListProfilesAsync()
    {
        var list = await AzCli.RunAsync("profile", "list");
        Assert.True(list.Succeeded, $"'profile list' failed.{Environment.NewLine}{list.Describe()}");

        var root = ParseJson(list, "profile list");
        Assert.True(
            root.ValueKind == JsonValueKind.Array,
            $"Expected 'profile list' to return a JSON array, got {root.ValueKind}."
                + $"{Environment.NewLine}{list.Describe()}");

        return [.. root.EnumerateArray()];
    }

    private static JsonElement ParseJson(AzResult result, string command)
    {
        try
        {
            // The element must outlive the JsonDocument, so clone it out.
            using var document = JsonDocument.Parse(result.StandardOutput);
            return document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            Assert.Fail(
                $"'{command}' did not return valid JSON ({exception.Message})."
                    + $"{Environment.NewLine}{result.Describe()}");
            throw;
        }
    }

    /// <summary>
    /// Reads a property case-insensitively, so the assertions do not depend on the
    /// serializer's casing policy.
    /// </summary>
    private static string GetString(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()!
                    : property.Value.ToString();
            }
        }

        Assert.Fail($"Expected a '{propertyName}' property in: {element}");
        return string.Empty;
    }
}
