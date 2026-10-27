// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using Core.Abstractions.Recovery;
using Core.Agent.Tools.Help;
using Xunit;

namespace Core.Agent.Tests;

public class AgentProcessHelpTests
{
    // Starts the built agent exe. The help prefetch for the failed command loads az.dll types
    // in the agent process. The invalid reasoning effort then stops the run before the
    // Copilot daemon or any model call starts.
    [Fact]
    public async Task RealAgentProcess_LoadsCliHelpTypesForFailedCommand()
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
        var home = Path.Combine(Path.GetTempPath(), "azclips-agent-help-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(home, "config.json"),
                """{"agent":{"provider":"github","reasoningEffort":"invalid","recovery":{"trigger":"manual"}},"telemetry":{"enabled":false}}""");
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
            start.ArgumentList.Add("--agent-mode");
            start.ArgumentList.Add(RecoveryRequest.InputPipeArgument);
            start.ArgumentList.Add(input.GetClientHandleAsString());
            using var process = Process.Start(start)!;
            input.DisposeLocalCopyOfClientHandle();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            try
            {
                await JsonSerializer.SerializeAsync(input, new RecoveryRequest
                {
                    FailedCommand = "config set --key core.output --value json",
                    ErrorText = "test error",
                    SessionStyle = "oneshot",
                }, cancellationToken: timeout.Token);
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
            Assert.DoesNotContain("Could not load file or assembly", text);
            Assert.DoesNotContain(nameof(FileNotFoundException), text);
            Assert.Contains("agent.reasoningEffort", text);
            Assert.True(File.Exists(Path.Combine(home, "agent", "help", HelpCatalogProvider.IndexFileName)), text);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

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
