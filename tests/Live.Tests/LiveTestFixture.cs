namespace Live.Tests;

/// <summary>
/// One-time preflight shared by every live test in the class.
/// <para>
/// Verifying the bundled CLI is present and runnable once, here, means a broken
/// bundle reports a single actionable error instead of the same low-level
/// <c>Win32Exception</c> repeated on every case.
/// </para>
/// </summary>
public sealed class LiveTestFixture : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        // The fixture is constructed even when every test is skipped, so it must
        // not touch configuration that is only present in a live run.
        if (!LiveTestEnvironment.IsEnabled)
        {
            return;
        }

        var az = Environment.GetEnvironmentVariable(LiveTestEnvironment.AzVariable);
        if (string.IsNullOrWhiteSpace(az))
        {
            return; // reported per-test as a skip by LiveFactAttribute
        }

        var path = LiveTestEnvironment.Az;
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"The CLI under test was not found at '{path}' (from {LiveTestEnvironment.AzVariable}). "
                + "Rebuild the bundle with scripts/publish-vm-mi.sh.");
        }

        var version = await AzCli.RunAsync("--version");
        if (!version.Succeeded)
        {
            throw new InvalidOperationException(
                $"The CLI at '{path}' could not be executed, so no live test can run."
                    + $"{Environment.NewLine}{version.Describe()}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Leave no credential cached on the VM after the suite finishes.
        if (LiveTestEnvironment.IsEnabled
            && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(LiveTestEnvironment.AzVariable)))
        {
            await AzCli.LogoutAsync();
        }
    }
}
