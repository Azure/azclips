// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Diagnostics;
using System.IO.Pipes;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Agent.AgentMode;
using Core.Agent.Hooks;
using Core.Agent.Hosting;
using GitHub.Copilot;
using Xunit;

namespace Core.Agent.Tests;

public class RecoveryInputTests
{
    [Theory]
    [InlineData(RecoveryTrigger.Automatic)]
    [InlineData(RecoveryTrigger.Manual)]
    public async Task Input_PreservesCompleteDiagnosticData(RecoveryTrigger trigger)
    {
        var request = CreateRequest() with { Trigger = trigger };
        await using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(request));

        var actual = await RecoveryInput.ReadRequestAsync(stream, CancellationToken.None);

        Assert.Equal(request, actual);
        var prompt = AgentModeEntry.BuildFirstPrompt(actual.FailedCommand, actual.ErrorText, AgentFlavor.AzureCli);
        Assert.Contains(request.FailedCommand, prompt);
        Assert.Contains(request.ErrorText, prompt);
        Assert.DoesNotContain(request.FailureId!, prompt);
    }

    [Theory]
    [InlineData("oneshot")]
    [InlineData("interactive")]
    public void Triggers_SharePromptAndEffectiveStyle(string style)
    {
        var options = new AgentOptions();
        options.Recovery.Style = style;
        var automatic = CreateRequest() with { Trigger = RecoveryTrigger.Automatic };
        var manual = automatic with { Trigger = RecoveryTrigger.Manual };

        Assert.Equal(
            AgentModeEntry.BuildFirstPrompt(automatic.FailedCommand, automatic.ErrorText, AgentFlavor.AzureCli),
            AgentModeEntry.BuildFirstPrompt(manual.FailedCommand, manual.ErrorText, AgentFlavor.AzureCli));
        Assert.Equal(
            RecoverySessionSettings.Resolve(options, automatic.Trigger).SessionStyle,
            RecoverySessionSettings.Resolve(options, manual.Trigger).SessionStyle);
        Assert.Equal("oneshot",
            RecoverySessionSettings.Resolve(options, manual.Trigger, "oneshot").SessionStyle);
        Assert.Equal(style, options.Recovery.Style);
    }

    [Theory]
    [InlineData("\u4e2d\u6587\n\"quoted\"")]
    public void Prompt_DoesNotNormalizeDiagnosticText(string error)
    {
        Assert.Contains(error, AgentModeEntry.BuildFirstPrompt("vm show", error, AgentFlavor.AzureCli));
    }

    [Theory]
    [InlineData(1, RecoveryTrigger.Manual)]
    [InlineData(3, RecoveryTrigger.Automatic)]
    [InlineData(2, (RecoveryTrigger)42)]
    public async Task Input_RejectsUnsupportedRequest(int version, RecoveryTrigger trigger)
    {
        await using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(CreateRequest() with
        {
            SchemaVersion = version,
            Trigger = trigger,
        }));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            RecoveryInput.ReadRequestAsync(stream, CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{invalid")]
    public async Task Input_RejectsIncompleteData(string json)
    {
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            RecoveryInput.ReadRequestAsync(stream, CancellationToken.None));
    }

    [Theory]
    [InlineData("invalid-tenant", "EU")]
    [InlineData(null, "untrusted-boundary")]
    public async Task Input_RejectsUntrustedRoutingValues(string? tenant, string? boundary)
    {
        await using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(CreateRequest() with
        {
            TenantId = tenant,
            DataBoundary = boundary,
        }));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            RecoveryInput.ReadRequestAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task LegacyArguments_DoNotOpenAPipe()
    {
        Assert.Null(await RecoveryInput.ReadAsync(
            ["--agent-mode", "--failed-command", "profile show", "--error", "failure"], CancellationToken.None));
    }

    [Fact]
    public async Task RealAgentProcess_ReadsLargePipeInputBeforeRejectingOffRecovery()
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
                """{"agent":{"recovery":{"trigger":"off"}},"telemetry":{"enabled":false}}""");
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
                await JsonSerializer.SerializeAsync(input, CreateRequest(),
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
            Assert.Contains("AI recovery is disabled", text);
            Assert.DoesNotContain("test-only-secret", text);
            Assert.DoesNotContain("Could not load", text);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Theory]
    [InlineData("oneshot", "bash")]
    [InlineData("oneshot", "powershell")]
    [InlineData("interactive", "bash")]
    [InlineData("interactive", "powershell")]
    public async Task Triggers_KeepTheSameUnapprovedExecutionGate(string style, string tool)
    {
        var options = new AgentOptions();
        options.Recovery.Style = style;
        foreach (var trigger in new[] { RecoveryTrigger.Automatic, RecoveryTrigger.Manual })
        {
            var settings = RecoverySessionSettings.Resolve(options, trigger);
            var state = new SessionState { IsOneShot = !settings.IsInteractive };
            var result = await PreToolUseHook.HandleAsync(
                new PreToolUseHookInput { ToolName = tool, ToolArgs = JsonSerializer.SerializeToElement(new { command = "az vm show" }) },
                "az", new ConsoleApprovalPrompt(), state, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal("deny", result?.PermissionDecision);
        }
    }

    [Fact]
    public async Task SharedGate_AllowsRegisteredReadOnlyTool()
    {
        var state = new SessionState { IsOneShot = true };

        var result = await PreToolUseHook.HandleAsync(
            new PreToolUseHookInput { ToolName = "get_command" },
            "az", new ConsoleApprovalPrompt(), state, CancellationToken.None);

        Assert.Null(result);
    }

    private static RecoveryRequest CreateRequest() => new()
    {
        FailureId = "1234-2026-0924-0810-0001",
        FailedCommand = "vm create --name \"with spaces\" \"\" \u4e2d\u6587",
        ErrorText = "test-only-secret\r\n" + new string('x', 80_000) + "\n\"quoted\"",
        SessionStyle = "oneshot",
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
