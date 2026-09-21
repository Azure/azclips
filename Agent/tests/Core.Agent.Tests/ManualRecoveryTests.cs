// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Diagnostics;
using System.IO.Pipes;
using Core.Abstractions.Recovery;
using Core.Agent.AgentMode;
using Core.Agent.Hooks;
using Core.Agent.Hosting;
using GitHub.Copilot;
using Xunit;

namespace Core.Agent.Tests;

public class ManualRecoveryTests
{
    [Fact]
    public async Task Input_PreservesCompleteDiagnosticData()
    {
        var failure = CreateFailure();
        await using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new RecoveryRequest { Failure = failure }));

        var actual = await ManualRecoveryInput.ReadRequestAsync(stream, CancellationToken.None);

        Assert.Equal(failure.Arguments, actual.Failure.Arguments);
        Assert.Equal(failure.ErrorText, actual.Failure.ErrorText);
        Assert.Equal(failure.Id, actual.Failure.Id);
        var prompt = ManualRecoveryInput.BuildPrompt(actual.Failure);
        using var data = JsonDocument.Parse(prompt[prompt.IndexOf('{')..]);
        Assert.Equal(failure.ErrorText, data.RootElement.GetProperty("error").GetString());
        Assert.Equal(failure.Arguments, data.RootElement.GetProperty("arguments").EnumerateArray().Select(arg => arg.GetString()));
    }

    [Theory]
    [InlineData(2, RecoveryTrigger.Manual)]
    [InlineData(1, RecoveryTrigger.Automatic)]
    public async Task Input_RejectsUnsupportedRequest(int version, RecoveryTrigger trigger)
    {
        await using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new RecoveryRequest
        {
            SchemaVersion = version,
            Trigger = trigger,
            Failure = CreateFailure(),
        }));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ManualRecoveryInput.ReadRequestAsync(stream, CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{invalid")]
    public async Task Input_RejectsIncompleteData(string json)
    {
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ManualRecoveryInput.ReadRequestAsync(stream, CancellationToken.None));
    }

    [Theory]
    [InlineData("invalid-tenant", "EU")]
    [InlineData(null, "untrusted-boundary")]
    public async Task Input_RejectsUntrustedRoutingValues(string? tenant, string? boundary)
    {
        await using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new RecoveryRequest
        {
            Failure = CreateFailure(),
            TenantId = tenant,
            DataBoundary = boundary,
        }));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ManualRecoveryInput.ReadRequestAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task LegacyArguments_DoNotOpenAPipe()
    {
        Assert.Null(await ManualRecoveryInput.ReadAsync(
            ["--agent-mode", "--failed-command", "profile show", "--error", "failure"], CancellationToken.None));
    }

    [Fact]
    public async Task RealAgentProcess_ReadsLargePipeInputBeforeRejectingDisabledAgent()
    {
        var root = FindRepositoryRoot();
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        var agentDirectory = Path.Combine(root, "artifacts", configuration, "Core.Agent");
        var agentExe = Path.Combine(agentDirectory, OperatingSystem.IsWindows() ? "azclips-agent.exe" : "azclips-agent");
        Assert.True(File.Exists(agentExe), $"Build the Agent project first: {agentExe}");
        var home = Path.Combine(Path.GetTempPath(), "azclips-manual-input-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(home, "config.json"),
                """{"agent":{"enabled":false},"telemetry":{"enabled":false}}""");
            using var input = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
            var start = new ProcessStartInfo(agentExe)
            {
                WorkingDirectory = agentDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.Environment["AZCLIPS_HOME"] = home;
            start.Environment["NO_COLOR"] = "1";
            start.Environment.Remove("AZCLIPS_RECOVERY_TRACEPARENT");
            start.ArgumentList.Add("--agent-mode");
            start.ArgumentList.Add(RecoveryRequest.InputPipeArgument);
            start.ArgumentList.Add(input.GetClientHandleAsString());
            using var process = Process.Start(start)!;
            input.DisposeLocalCopyOfClientHandle();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await JsonSerializer.SerializeAsync(input, new RecoveryRequest { Failure = CreateFailure() },
                    cancellationToken: timeout.Token);
                await input.DisposeAsync();
                await process.WaitForExitAsync(timeout.Token);
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }
            }

            var text = await output + await error;
            Assert.Equal(1, process.ExitCode);
            Assert.Contains("Agent features are disabled", text);
            Assert.DoesNotContain("test-only-secret", text);
            Assert.DoesNotContain("Could not load", text);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Theory]
    [InlineData("bash")]
    [InlineData("powershell")]
    [InlineData("edit")]
    [InlineData("write")]
    [InlineData("get_unregistered_tool")]
    public async Task ManualGate_DeniesNonDiagnosticToolsEvenWithAnApprovedPlan(string tool)
    {
        var state = new SessionState
        {
            IsManualRecovery = true,
            IsOneShot = true,
            PlanApproved = true,
            AllowedManualTools = new HashSet<string>(["get_command", "propose_plan", "submit_response"], StringComparer.OrdinalIgnoreCase),
        };
        var result = await PreToolUseHook.HandleAsync(
            new PreToolUseHookInput { ToolName = tool, ToolArgs = JsonSerializer.SerializeToElement(new { command = "do-not-run" }) },
            "az", new ConsoleApprovalPrompt(), state, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("deny", result?.PermissionDecision);
    }

    [Fact]
    public async Task ManualGate_AllowsRegisteredReadOnlyTool()
    {
        var state = new SessionState
        {
            IsManualRecovery = true,
            IsOneShot = true,
            AllowedManualTools = new HashSet<string>(["get_command"], StringComparer.OrdinalIgnoreCase),
        };

        var result = await PreToolUseHook.HandleAsync(
            new PreToolUseHookInput { ToolName = "get_command" },
            "az", new ConsoleApprovalPrompt(), state, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void ManualTerminalSdkError_IsNotSuccess()
    {
        var error = new SessionErrored("test", "synthetic failure", null, null, null, DateTimeOffset.UtcNow);

        Assert.Equal(1, AgentModeEntry.GetManualExitCode(error, CancellationToken.None));
        Assert.Equal(130, AgentModeEntry.GetManualExitCode(null, new CancellationToken(canceled: true)));
        Assert.Equal(0, AgentModeEntry.GetManualExitCode(null, CancellationToken.None));
        Assert.Equal(1, AgentModeEntry.GetManualExitCode(null, CancellationToken.None, hasResult: false));
    }

    private static FailureRecord CreateFailure() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        OccurredAtUtc = DateTimeOffset.UtcNow,
        Arguments = ["vm", "create", "--name", "with spaces", "", "test-only-secret", "\u4e2d\u6587"],
        FailedCommand = "vm create --name \"with spaces\"",
        ErrorText = "test-only-secret\r\n" + new string('x', 80_000) + "\n\"quoted\"",
        ExitCode = 1,
    };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "azclips.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
