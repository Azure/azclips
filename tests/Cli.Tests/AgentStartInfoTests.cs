// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Runtime.Recovery;
using Xunit;

namespace Cli.Tests;

/// <summary>Tests explicit telemetry launch context and removal of legacy handoff variables.</summary>
[Collection("Azclips Service Provider Tests")]
public class AgentStartInfoTests
{
    private const string Traceparent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    private static string AgentExe => Path.Combine(AppContext.BaseDirectory, "azclips-agent.exe");


    private static TelemetryInvocationContext Context(bool enabled = true) =>
        new(Guid.NewGuid().ToString("N"), enabled,
            "4bf92f3577b34da6a3ce929d0e0e4736", "00f067aa0ba902b7", "profile show",
            DataBoundary: "EU");

    [Fact]
    public void BuildAgentStartInfo_ForwardsCorrelationAndCallerGeneratedFileIdWithoutAParent()
    {
        var context = Context();
        var fileId = Guid.NewGuid().ToString("N");
        var psi = AgentLauncher.BuildAgentStartInfo(AgentExe, "7", context, fileId);

        Assert.True(TelemetryLayerEnvironment.TryReadAgentLaunch(
            key => psi.Environment.TryGetValue(key, out var value) ? value : null,
            out var actual, out var actualFileId, out var diagnostic));
        Assert.Equal(context, actual);
        Assert.Equal(fileId, actualFileId);
        Assert.Null(diagnostic);
        Assert.False(psi.Environment.ContainsKey("AZCLIPS_RECOVERY_TRACEPARENT"));
        Assert.False(psi.Environment.ContainsKey("AZCLIPS_RECOVERY_HIERARCHY"));
    }

    [Fact]
    public void BuildAgentStartInfo_CarriesExplicitOptOutWithoutReusingTheOldParent()
    {
        var psi = AgentLauncher.BuildAgentStartInfo(
            AgentExe, "7", Context(enabled: false), Guid.NewGuid().ToString("N"));
        Assert.Equal("false", psi.Environment[TelemetryLayerEnvironment.Enabled]);
        Assert.False(psi.Environment.ContainsKey("AZCLIPS_RECOVERY_TRACEPARENT"));
        Assert.False(psi.Environment.ContainsKey("AZCLIPS_RECOVERY_HIERARCHY"));
    }

    [Fact]
    public void BuildAgentStartInfo_RemovesInheritedHandoffWhenTelemetryIsDisabled()
    {
        var savedTraceparent = Environment.GetEnvironmentVariable("AZCLIPS_RECOVERY_TRACEPARENT");
        var savedHierarchy = Environment.GetEnvironmentVariable("AZCLIPS_RECOVERY_HIERARCHY");
        var savedFile = Environment.GetEnvironmentVariable(TelemetryLayerEnvironment.FileId);
        try
        {
            Environment.SetEnvironmentVariable("AZCLIPS_RECOVERY_TRACEPARENT", Traceparent);
            Environment.SetEnvironmentVariable("AZCLIPS_RECOVERY_HIERARCHY", "stale hierarchy");
            Environment.SetEnvironmentVariable(TelemetryLayerEnvironment.FileId, "inherited-file");

            var fileId = Guid.NewGuid().ToString("N");
            var psi = AgentLauncher.BuildAgentStartInfo(AgentExe, "7", Context(enabled: false), fileId);

            Assert.False(psi.Environment.ContainsKey("AZCLIPS_RECOVERY_TRACEPARENT"));
            Assert.False(psi.Environment.ContainsKey("AZCLIPS_RECOVERY_HIERARCHY"));
            Assert.Equal(fileId, psi.Environment[TelemetryLayerEnvironment.FileId]);
            Assert.Equal(Traceparent, Environment.GetEnvironmentVariable("AZCLIPS_RECOVERY_TRACEPARENT"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZCLIPS_RECOVERY_TRACEPARENT", savedTraceparent);
            Environment.SetEnvironmentVariable("AZCLIPS_RECOVERY_HIERARCHY", savedHierarchy);
            Environment.SetEnvironmentVariable(TelemetryLayerEnvironment.FileId, savedFile);
        }
    }

    [Fact]
    public void BuildAgentStartInfo_KeepsTheExistingSpawnShape()
    {
        var psi = AgentLauncher.BuildAgentStartInfo(AgentExe, "7", Context(), Guid.NewGuid().ToString("N"));

        // Recursion guard: a failed `az` invoked from inside the agent must not spawn another.
        Assert.Equal("1", psi.Environment["AZCLIPS_IN_AGENT_MODE"]);
        // Environment overrides only apply when the process is not shell-executed.
        Assert.False(psi.UseShellExecute);
        // Stdio is inherited so streaming tokens and the y/n prompt reach the user's terminal.
        Assert.False(psi.RedirectStandardOutput);
        Assert.False(psi.RedirectStandardError);
        Assert.False(psi.RedirectStandardInput);
        Assert.Equal(AgentExe, psi.FileName);
        Assert.Equal(new[] { "--agent-mode", "--recovery-input", "7" }, psi.ArgumentList);
    }

    [Fact]
    public void RecoveryStartInfo_UsesTheSavedFailureDirectory()
    {
        var saved = Path.Combine(Path.GetTempPath(), "saved-failure-dir");
        var psi = AgentLauncher.BuildAgentStartInfo(AgentExe, "7", Context(), Guid.NewGuid().ToString("N"), saved);

        Assert.Equal(saved, psi.Environment[AgentLauncher.CallerCwdEnvVar]);
    }

    [Fact]
    public void RecoveryStartInfo_UsesTheCurrentDirectory_WhenNoDirectoryWasSaved()
    {
        var psi = AgentLauncher.BuildAgentStartInfo(AgentExe, "7", Context(), Guid.NewGuid().ToString("N"));

        Assert.Equal(Directory.GetCurrentDirectory(), psi.Environment[AgentLauncher.CallerCwdEnvVar]);
    }

    [Theory]
    [InlineData(new[] { "profile", "show", "my-customer-prod" }, "profile")]
    [InlineData(new[] { "extension", "show", "Compute" }, "extension")]
    [InlineData(new[] { "vm", "create", "--name", "myvm", "--admin-password", "hunter2" }, "vm")]
    [InlineData(new[] { "--subscription", "secret" }, "")]
    [InlineData(new string[0], "")]
    public void GetCommandGroupFromArgs_EmitsTheGroupOnly(string[] args, string expected)
    {
        var commandGroup = Core.Cli.Program.GetCommandGroupFromArgs(args);

        Assert.Equal(expected, commandGroup);
        foreach (var value in new[] { "my-customer-prod", "Compute", "myvm", "hunter2", "secret" })
        {
            Assert.DoesNotContain(value, commandGroup, StringComparison.Ordinal);
        }
    }
}
