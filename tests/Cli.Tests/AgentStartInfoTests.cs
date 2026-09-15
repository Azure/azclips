// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// The agent runs in its own process, so its spans join the CLI.Activity.AiRecovery
/// trace through a W3C traceparent on the child environment block.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class AgentStartInfoTests
{
    private const string Traceparent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    private static string AgentExe => Path.Combine(AppContext.BaseDirectory, "azclips-agent.exe");


    [Fact]
    public void RecoveryTraceparentEnvVar_UsesTheSharedLiteral()
    {
        // Keep this literal pinned here; Core.Agent.Tests asserts the counterpart on the
        // agent side because neither test project can see both internals.
        Assert.Equal("AZCLIPS_RECOVERY_TRACEPARENT", Core.Cli.Program.RecoveryTraceparentEnvVar);
    }

    [Fact]
    public void BuildAgentStartInfo_ForwardsTheRecoveryTraceparent()
    {
        var psi = Core.Cli.Program.BuildAgentStartInfo(
            AgentExe, new[] { "profile", "show" }, "boom", Traceparent);

        Assert.Equal(Traceparent, psi.Environment[Core.Cli.Program.RecoveryTraceparentEnvVar]);
    }

    [Fact]
    public void BuildAgentStartInfo_ForwardsTheSourceRecordedHandoffWithoutEditingIt()
    {
        using var source = new ActivitySource("handoff-" + Guid.NewGuid());
        using var listener = new ActivityListener
        {
            ShouldListenTo = candidate => candidate == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        using var recovery = TelemetryHierarchy.StartActivity(source, "CLI.Activity.AiRecovery");
        Assert.NotNull(recovery);
        var handoff = TelemetryHierarchy.CreateRecoveryHandoff(recovery);

        var psi = Core.Cli.Program.BuildAgentStartInfo(
            AgentExe, ["profile", "show"], "private error", recovery.Id, handoff);

        Assert.Equal(recovery.Id, psi.Environment[TelemetryHierarchy.RecoveryTraceparentEnvVar]);
        Assert.Equal(handoff, psi.Environment[TelemetryHierarchy.RecoveryHierarchyEnvVar]);
        Assert.DoesNotContain("private error", handoff);
        Assert.True(TelemetryHierarchy.TryReadRecoveryHandoff(recovery.Context,
            psi.Environment[TelemetryHierarchy.RecoveryHierarchyEnvVar], out var parent));
        Assert.Equal(recovery.SpanId, parent!.ActivityContext.SpanId);
        Assert.Equal(0, parent.Depth);
        Assert.Equal("Agent", parent.Path);
        Assert.Equal("CLI.Activity.AiRecovery", recovery.OperationName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BuildAgentStartInfo_OmitsTheTraceparent_WhenTelemetryProducedNoSpan(string? traceparent)
    {
        // StartRootActivity returns null when telemetry.enabled=false, so Id is null.
        // An empty value would be parsed by the agent and rejected; absent is cleaner.
        var psi = Core.Cli.Program.BuildAgentStartInfo(
            AgentExe, new[] { "profile", "show" }, "boom", traceparent);

        Assert.False(psi.Environment.ContainsKey(Core.Cli.Program.RecoveryTraceparentEnvVar));
        Assert.False(psi.Environment.ContainsKey(TelemetryHierarchy.RecoveryHierarchyEnvVar));
    }

    [Fact]
    public void BuildAgentStartInfo_DoesNotForwardHierarchyWithoutTraceparent()
    {
        var psi = Core.Cli.Program.BuildAgentStartInfo(
            AgentExe, [], null, null, "{\"Version\":1}");

        Assert.False(psi.Environment.ContainsKey(TelemetryHierarchy.RecoveryTraceparentEnvVar));
        Assert.False(psi.Environment.ContainsKey(TelemetryHierarchy.RecoveryHierarchyEnvVar));
    }

    [Fact]
    public void BuildAgentStartInfo_RemovesInheritedHandoffWhenTelemetryIsDisabled()
    {
        var savedTraceparent = Environment.GetEnvironmentVariable(TelemetryHierarchy.RecoveryTraceparentEnvVar);
        var savedHierarchy = Environment.GetEnvironmentVariable(TelemetryHierarchy.RecoveryHierarchyEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(TelemetryHierarchy.RecoveryTraceparentEnvVar, Traceparent);
            Environment.SetEnvironmentVariable(TelemetryHierarchy.RecoveryHierarchyEnvVar, "stale hierarchy");

            var psi = Core.Cli.Program.BuildAgentStartInfo(AgentExe, [], null, null);

            Assert.False(psi.Environment.ContainsKey(TelemetryHierarchy.RecoveryTraceparentEnvVar));
            Assert.False(psi.Environment.ContainsKey(TelemetryHierarchy.RecoveryHierarchyEnvVar));
            Assert.Equal(Traceparent, Environment.GetEnvironmentVariable(TelemetryHierarchy.RecoveryTraceparentEnvVar));
        }
        finally
        {
            Environment.SetEnvironmentVariable(TelemetryHierarchy.RecoveryTraceparentEnvVar, savedTraceparent);
            Environment.SetEnvironmentVariable(TelemetryHierarchy.RecoveryHierarchyEnvVar, savedHierarchy);
        }
    }

    [Fact]
    public void BuildAgentStartInfo_KeepsTheExistingSpawnShape()
    {
        var psi = Core.Cli.Program.BuildAgentStartInfo(
            AgentExe, new[] { "profile", "show" }, "boom", Traceparent);

        // Recursion guard: a failed `az` invoked from inside the agent must not spawn another.
        Assert.Equal("1", psi.Environment["AZCLIPS_IN_AGENT_MODE"]);
        // Environment overrides only apply when the process is not shell-executed.
        Assert.False(psi.UseShellExecute);
        // Stdio is inherited so streaming tokens and the y/n prompt reach the user's terminal.
        Assert.False(psi.RedirectStandardOutput);
        Assert.False(psi.RedirectStandardError);
        Assert.False(psi.RedirectStandardInput);
        Assert.Equal(AgentExe, psi.FileName);
        Assert.Equal(
            new[] { "--agent-mode", "--failed-command", "profile show", "--error", "boom" },
            psi.ArgumentList);
    }

    [Fact]
    public void BuildAgentStartInfo_OmitsTheErrorFlag_WhenThereIsNoErrorText()
    {
        var psi = Core.Cli.Program.BuildAgentStartInfo(
            AgentExe, new[] { "profile", "show" }, null, Traceparent);

        Assert.Equal(new[] { "--agent-mode", "--failed-command", "profile show" }, psi.ArgumentList);
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
