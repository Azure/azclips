// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Core.Abstractions;
using Core.Agent.Providers;
using GitHub.Copilot;
using Tests.Common.Stubs;
using Xunit.Abstractions;

namespace Core.Agent.Tests;

/// <summary>
/// Compares the text of the SDK sections we keep against the committed baselines in
/// <c>Baselines/</c>, so a Copilot runtime upgrade that rewrites them fails here.
/// Baselines are per platform; when the current platform has none, the test passes after
/// writing a skip message, because xunit 2 has no runtime skip.
/// </summary>
public sealed class SystemPromptFingerprintTests(ITestOutputHelper output)
{
    // LocalOnly: starts the bundled Copilot runtime, which CI does not run.
    [Trait("Category", "LocalOnly")]
    [Theory]
    [InlineData("oneshot")]
    [InlineData("interactive")]
    public async Task KeptSdkSections_MatchBaseline(string style)
    {
        var baseline = Path.Combine(TestDirectory(), "Baselines",
            $"{(style == "oneshot" ? "oneshot" : "inline")}-sections.{CurrentPlatform()}.txt");
        if (!File.Exists(baseline))
        {
            output.WriteLine($"SKIPPED: no baseline for this platform at {baseline}.");
            return;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var options = new AgentOptions { Provider = "github", Recovery = new() { Style = style } };
        var built = await SessionConfigTests.BuildAsync(options, new StubAuthService());
        var kept = typeof(SystemMessageSection).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(SystemMessageSection))
            .Select(p => (SystemMessageSection)p.GetValue(null)!)
            .Where(s => !built.SystemMessage!.Sections!.ContainsKey(s)
                && s != SystemMessageSection.Preamble && s != SystemMessageSection.ToolEfficiency)
            .ToList();
        var captured = new ConcurrentDictionary<string, string>();
        var allCaptured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sections = new Dictionary<SystemMessageSection, SectionOverride>(built.SystemMessage!.Sections!);
        foreach (var section in kept)
        {
            sections[section] = new()
            {
                Transform = content =>
                {
                    captured[section.Value] = content;
                    if (captured.Count == kept.Count) allCaptured.TrySetResult();
                    return Task.FromResult(content);
                },
            };
        }

        await using var client = new CopilotClient(new CopilotClientOptions
        {
            Connection = RuntimeConnection.ForStdio(path: BundledDaemonResolver.Resolve()),
            UseLoggedInUser = false,
        });
        // Same config directory and tools as ChatAgent, with the builtin provider: the section
        // text depends on the model and on GitHub sign-in, so the signed-in defaults must not apply.
        // The system message is rendered before the first request, so the token is never used.
        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            SessionId = Guid.NewGuid().ToString(),
            ConfigDirectory = built.ConfigDirectory,
            Model = options.Builtin.ModelId,
            Provider = BuiltinProviderConfig.Build(options.Builtin, BuiltinEndpointResolver.Choose(boundary: null).Url, "capture-only"),
            Tools = built.Tools,
            AvailableTools = built.AvailableTools,
            SystemMessage = new SystemMessageConfig { Mode = SystemMessageMode.Customize, Sections = sections },
            OnPermissionRequest = PermissionHandler.ApproveAll,
        }, timeout.Token);
        // The runtime renders the system message before the first model request.
        await session.SendAsync(new MessageOptions { Prompt = "Reply with OK." }, timeout.Token);
        await allCaptured.Task.WaitAsync(timeout.Token);
        await session.AbortAsync(timeout.Token);

        var text = new StringBuilder();
        foreach (var section in kept.Select(s => s.Value).Order())
            text.Append("=== ").Append(section).Append(" ===\n").Append(Normalize(captured[section])).Append('\n');
        Assert.Equal(File.ReadAllText(baseline).Replace("\r\n", "\n"), text.ToString());
    }

    private static string TestDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") =>
        Path.GetDirectoryName(path)!;

    private static string CurrentPlatform() =>
        OperatingSystem.IsWindows() ? "windows"
        : OperatingSystem.IsLinux() ? "linux"
        : OperatingSystem.IsMacOS() ? "macos"
        : "unknown";

    // The "Git repository" line appears only for some origin remotes, so it is dropped.
    private static string Normalize(string content) =>
        Regex.Replace(Regex.Replace(content.Replace("\r\n", "\n"), @"^\* Git repository: .*\n", "", RegexOptions.Multiline),
            @"^(\* (Current working directory|Git repository root|Session folder|Available tools)):.*$", "$1: <dynamic>",
            RegexOptions.Multiline);
}
