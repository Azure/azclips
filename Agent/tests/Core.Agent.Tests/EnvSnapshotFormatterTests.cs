using Core.Agent.Common;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Contract tests for <see cref="EnvSnapshotFormatter"/> — takes the raw JSON
/// emitted by <c>az agent tool env show</c> and turns it into the
/// <c>&lt;environment&gt;</c> block the model sees. Assert on visible-to-model
/// substrings so a rename of a JSON key or a missing field is caught, but not
/// on exact wrapper-string phrasing.
/// </summary>
public class EnvSnapshotFormatterTests
{
    [Fact]
    public void SignedInSnapshot_IncludesSubTenantFlavorVersion()
    {
        const string json = """
            {
              "os": "Windows 11",
              "shell": "pwsh",
              "cwd": "C:/work",
              "sshSession": false,
              "browserAvailable": true,
              "stdinInteractive": true,
              "signedIn": true,
              "subscriptionId": "sub-1",
              "tenantId": "tenant-1",
              "flavor": "azure cli",
              "azclipsVersion": "0.1.0"
            }
            """;

        var block = EnvSnapshotFormatter.Format(json);

        Assert.Contains("<environment>", block);
        Assert.Contains("</environment>", block);
        Assert.Contains("os: Windows 11", block);
        Assert.Contains("shell: pwsh", block);
        Assert.Contains("signed-in: true", block);
        Assert.Contains("subscription-id: sub-1", block);
        Assert.Contains("tenant-id: tenant-1", block);
        Assert.Contains("version: 0.1.0", block);
        Assert.Contains("flavor: azure cli", block);
    }

    [Fact]
    public void NotSignedIn_MissingSubTenantShowNone()
    {
        const string json = """
            {
              "os": "Linux",
              "shell": "bash",
              "cwd": "/home/u",
              "sshSession": false,
              "browserAvailable": false,
              "stdinInteractive": true,
              "signedIn": false,
              "subscriptionId": null,
              "tenantId": null,
              "flavor": "azure cli",
              "azclipsVersion": "0.1.0"
            }
            """;

        var block = EnvSnapshotFormatter.Format(json);

        Assert.Contains("signed-in: false", block);
        Assert.Contains("subscription-id: (none)", block);
        Assert.Contains("tenant-id: (none)", block);
    }

    [Fact]
    public void ShellOutErrorPayload_ProducesEmptyBlock()
    {
        // ShellOutHelper serializes failures as { "error": "...", ... }. Injecting
        // that as env context would confuse the model; degrade to an empty block.
        const string json = """{"error":"shell_out_nonzero_exit","exitCode":1}""";

        var block = EnvSnapshotFormatter.Format(json);

        Assert.Equal(string.Empty, block);
    }

    [Fact]
    public void MalformedJson_ProducesEmptyBlock()
    {
        var block = EnvSnapshotFormatter.Format("not json at all");
        Assert.Equal(string.Empty, block);
    }

    [Fact]
    public void ValidJsonButNotObject_ProducesEmptyBlock()
    {
        // Valid JSON that parses fine but isn't a JSON object hits the
        // ValueKind != Object guard (a different branch than the parse-failure
        // catch above). A JSON array must still yield an empty block.
        var block = EnvSnapshotFormatter.Format("[1, 2, 3]");
        Assert.Equal(string.Empty, block);
    }

    [Fact]
    public void CommandGroups_AppearInEnvironmentBlock()
    {
        const string json = """
            { "signedIn": true, "commandGroups": ["group", "network", "vm"] }
            """;

        var block = EnvSnapshotFormatter.Format(json);

        Assert.Contains("command-groups", block);
        Assert.Contains("group, network, vm", block);
        Assert.True(block.IndexOf("group, network, vm", StringComparison.Ordinal)
                    < block.IndexOf("</environment>", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{ "signedIn": true, "commandGroups": [] }""")]
    [InlineData("""{ "signedIn": true }""")]
    public void CommandGroups_EmptyOrMissing_OmitsSection(string json)
    {
        var block = EnvSnapshotFormatter.Format(json);

        Assert.Contains("<environment>", block);
        Assert.DoesNotContain("command-groups", block);
    }
}
