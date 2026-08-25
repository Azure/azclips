using System.Text;
using Live.Tool;

namespace Live.Tool.Tests;

/// <summary>
/// The generated script is written on the host and executed by bash inside the Linux VM.
/// Windows line endings or a UTF-8 BOM make bash fail with errors that name the wrong
/// thing entirely (<c>$'\r': command not found</c>, or a complaint about the shebang),
/// which is exactly the class of breakage this port could introduce.
/// </summary>
public class RemoteScriptTests
{
    private static string Build(
        string uamiClientId = "11111111-1111-1111-1111-111111111111",
        string uamiResourceId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/u",
        string uamiObjectId = "22222222-2222-2222-2222-222222222222") =>
        RemoteScript.Build(
            blobUrl: "https://milive0123.blob.core.windows.net/bundle/x.tar.gz?sig=abc",
            bundleName: "Release-linux-x64",
            vmName: "mxaa-azclips-live-test-mi-vm",
            resourceGroup: "mxaa-azclips-live-test-mi-rg",
            subscriptionId: "b813fd05-4867-4dde-9efc-5e34b5373b57",
            uamiClientId: uamiClientId,
            uamiResourceId: uamiResourceId,
            uamiObjectId: uamiObjectId);

    [Fact]
    public void Build_UsesUnixLineEndingsOnly()
    {
        Assert.DoesNotContain('\r', Build());
    }

    [Fact]
    public void Build_ContainsNoBomCharacter()
    {
        Assert.DoesNotContain('\uFEFF', Build());
    }

    [Fact]
    public void Build_EncodesWithoutABomWhenWritten()
    {
        // Belt and braces: the string can be clean while the file on disk is not.
        var path = Path.Combine(Path.GetTempPath(), $"livetool-{Guid.NewGuid():N}.sh");
        try
        {
            RemoteScript.Write(path, Build());
            var bytes = File.ReadAllBytes(path);

            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            Assert.DoesNotContain((byte)'\r', bytes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Build_FailsFastInsideTheVm()
    {
        // run-command reports success from the *invocation*, not the script, so without
        // set -e a failed download would fall through to a PASS sentinel.
        Assert.Contains("set -eu", Build(), StringComparison.Ordinal);
        // pipefail is bash-only; run-command uses dash, so it must not be *set* here.
        Assert.DoesNotContain("set -eo pipefail", Build(), StringComparison.Ordinal);
        Assert.DoesNotContain("set -euo pipefail", Build(), StringComparison.Ordinal);
    }

    [Fact]
    public void Build_EmitsBothResultSentinels()
    {
        // The orchestrator decides pass/fail purely by grepping for these.
        var script = Build();
        Assert.Contains("MI_LIVE_RESULT:PASS", script, StringComparison.Ordinal);
        Assert.Contains("MI_LIVE_RESULT:FAIL", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ForwardsUserAssignedIdentitySelectors()
    {
        var script = Build();
        Assert.Contains("--uami-client-id '11111111-1111-1111-1111-111111111111'", script, StringComparison.Ordinal);
        Assert.Contains("--uami-object-id '22222222-2222-2222-2222-222222222222'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_OmitsIdentitySelectorsThatAreNotConfigured()
    {
        // The UAMI tests are currently skipped, so these arrive empty; passing
        // `--uami-client-id ''` would make the CLI reject the whole invocation.
        var script = Build(uamiClientId: "", uamiResourceId: "", uamiObjectId: "");

        Assert.DoesNotContain("--uami-", script, StringComparison.Ordinal);
        Assert.Contains("--test-mi", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_AsksTheGuestForTimingMarkers()
    {
        // Without this the guest prints its own table, which run-command's 4KB tail then
        // truncates away along with the result sentinel.
        Assert.Contains("TIMING_EMIT_MARKERS=1", Build(), StringComparison.Ordinal);
    }
}
