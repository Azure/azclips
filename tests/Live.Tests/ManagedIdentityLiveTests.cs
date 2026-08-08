using System.Text.Json;

namespace Live.Tests;

/// <summary>
/// End-to-end managed-identity tests that run <b>on a real Azure VM</b>.
///
/// <para>
/// Each case follows the same shape: clear any cached credential, log in with one
/// managed-identity selector, then make an authenticated ARM call and assert it
/// returned the VM we expect. Asserting on the returned resource matters — a token
/// issued for the wrong principal can still produce a well-formed response, so a
/// bare "exit code 0" would not prove the right identity authenticated.
/// </para>
///
/// <para>
/// The counterpart suite <c>Cli.Tests.ManagedIdentityE2ETests</c> covers the same
/// verbs against a mock IMDS server. This one exists to catch what a mock cannot:
/// real token issuance, real RBAC evaluation, and the self-contained bundle
/// actually running on a machine with no .NET installed.
/// </para>
/// </summary>
public class ManagedIdentityLiveTests : IClassFixture<LiveTestFixture>, IAsyncLifetime
{
    // Role assignments are eventually consistent: a freshly created assignment can
    // take a couple of minutes to be honoured by ARM, so the first authenticated
    // call after provisioning is retried rather than treated as a failure.
    private const int MaxAttempts = 12;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Clears any cached credential before each case. Without this a test could
    /// pass using the identity a previous test logged in as, which would make the
    /// selector under test irrelevant and the assertion meaningless.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        if (!LiveTestEnvironment.IsEnabled)
        {
            return; // skipped cases still construct the class
        }

        var logout = await AzCli.LogoutAsync();
        Assert.True(
            logout.Succeeded,
            "Could not clear the existing credential before the test, so the result could "
                + $"reflect a previously logged-in identity.{Environment.NewLine}{logout.Describe()}");
    }

    /// <summary>
    /// Best-effort cleanup. Failures here are deliberately ignored: the fixture
    /// logs out once more at the end, and throwing would mask the real result.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (LiveTestEnvironment.IsEnabled)
        {
            await AzCli.LogoutAsync();
        }
    }

    [LiveFact]
    public async Task Login_WithSystemAssignedIdentity_CanReadOwnVm()
    {
        await AssertIdentityCanReadOwnVmAsync("system-assigned");
    }

    [LiveFact(RequiresVariables = [LiveTestEnvironment.UamiClientIdVariable])]
    public async Task Login_WithUserAssignedIdentityByClientId_CanReadOwnVm()
    {
        await AssertIdentityCanReadOwnVmAsync(
            "user-assigned/client-id", "--client-id", LiveTestEnvironment.UamiClientId);
    }

    [LiveFact(RequiresVariables = [LiveTestEnvironment.UamiResourceIdVariable])]
    public async Task Login_WithUserAssignedIdentityByResourceId_CanReadOwnVm()
    {
        await AssertIdentityCanReadOwnVmAsync(
            "user-assigned/resource-id", "--resource-id", LiveTestEnvironment.UamiResourceId);
    }

    [LiveFact(RequiresVariables = [LiveTestEnvironment.UamiObjectIdVariable])]
    public async Task Login_WithUserAssignedIdentityByObjectId_CanReadOwnVm()
    {
        await AssertIdentityCanReadOwnVmAsync(
            "user-assigned/object-id", "--object-id", LiveTestEnvironment.UamiObjectId);
    }

    /// <summary>
    /// A selector naming an identity that is not assigned to this VM must fail.
    /// Without this, a bug that silently ignored the selector and fell back to the
    /// system-assigned identity would still make every positive case above pass.
    /// </summary>
    [LiveFact]
    public async Task Login_WithUnassignedClientId_Fails()
    {
        var unassignedClientId = Guid.NewGuid().ToString();
        var login = await AzCli.LoginWithIdentityAsync("--client-id", unassignedClientId);

        if (!login.Succeeded)
        {
            return;
        }

        // Some IMDS failures only surface when the token is first used, so an
        // apparently successful login still has to be probed with a real call.
        var show = await ShowVmAsync();
        Assert.False(
            show.Succeeded,
            $"Expected login with an unassigned client id ({unassignedClientId}) to fail, "
                + $"but the CLI authenticated and read the VM. This suggests the selector was "
                + $"ignored and another identity was used.{Environment.NewLine}{show.Describe()}");
    }

    private static async Task AssertIdentityCanReadOwnVmAsync(string label, params string[] selectorArgs)
    {
        var login = await AzCli.LoginWithIdentityAsync(selectorArgs);
        Assert.True(
            login.Succeeded,
            $"[{label}] 'az login --identity {string.Join(' ', selectorArgs)}' "
                + $"failed.{Environment.NewLine}{login.Describe()}");

        var show = await ShowVmWithRetryAsync(label);
        var name = ReadVmName(show, label);

        Assert.True(
            string.Equals(name, LiveTestEnvironment.VmName, StringComparison.OrdinalIgnoreCase),
            $"[{label}] authenticated, but 'vm show' returned '{name}' instead of "
                + $"'{LiveTestEnvironment.VmName}'. The token may belong to a different identity.");
    }

    private static Task<AzResult> ShowVmAsync() =>
        AzCli.RunAsync(
            "vm", "show",
            "--name", LiveTestEnvironment.VmName,
            "--resource-group", LiveTestEnvironment.ResourceGroup);

    private static async Task<AzResult> ShowVmWithRetryAsync(string label)
    {
        AzResult result = null!;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            result = await ShowVmAsync();
            if (result.Succeeded)
            {
                return result;
            }

            if (attempt < MaxAttempts)
            {
                await Task.Delay(RetryDelay);
            }
        }

        Assert.Fail(
            $"[{label}] 'vm show' never succeeded after {MaxAttempts} attempts over "
                + $"{MaxAttempts * RetryDelay.TotalSeconds / 60:N0} minutes, so this is not "
                + $"RBAC propagation delay.{Environment.NewLine}{result.Describe()}");
        return result;
    }

    private static string ReadVmName(AzResult result, string label)
    {
        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            if (document.RootElement.TryGetProperty("name", out var name)
                && name.ValueKind == JsonValueKind.String)
            {
                return name.GetString()!;
            }
        }
        catch (JsonException exception)
        {
            Assert.Fail(
                $"[{label}] 'vm show' did not return valid JSON ({exception.Message})."
                    + $"{Environment.NewLine}{result.Describe()}");
        }

        Assert.Fail(
            $"[{label}] 'vm show' returned JSON without a string 'name' property."
                + $"{Environment.NewLine}{result.Describe()}");
        return string.Empty;
    }
}
