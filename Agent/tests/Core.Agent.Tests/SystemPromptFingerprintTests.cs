// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Core.Abstractions;
using GitHub.Copilot;
using Tests.Common.Stubs;

namespace Core.Agent.Tests;

/// <summary>
/// Compares the text of the SDK sections we keep against the committed baselines in
/// <c>Baselines/</c>, so a Copilot runtime upgrade that rewrites them fails here.
/// </summary>
public sealed class SystemPromptFingerprintTests
{
    // LocalOnly: the runtime renders the system message only for a signed-in GitHub user.
    [Trait("Category", "LocalOnly")]
    [Theory]
    [InlineData("oneshot")]
    [InlineData("interactive")]
    public async Task KeptSdkSections_MatchBaseline(string style)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var built = await SessionConfigTests.BuildAsync(
            new AgentOptions { Provider = "github", Recovery = new() { Style = style } }, new StubAuthService());
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
        });
        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            SessionId = Guid.NewGuid().ToString(),
            SystemMessage = new SystemMessageConfig { Mode = SystemMessageMode.Customize, Sections = sections },
            AvailableTools = built.AvailableTools,
            OnPermissionRequest = PermissionHandler.ApproveAll,
        }, timeout.Token);
        // The runtime renders the system message before the first model request.
        await session.SendAsync(new MessageOptions { Prompt = "Reply with OK." }, timeout.Token);
        await allCaptured.Task.WaitAsync(timeout.Token);
        await session.AbortAsync(timeout.Token);

        var text = new StringBuilder();
        foreach (var section in kept.Select(s => s.Value).Order())
            text.Append("=== ").Append(section).Append(" ===\n").Append(Normalize(captured[section])).Append('\n');
        var baseline = Path.Combine(TestDirectory(), "Baselines", (style == "oneshot" ? "oneshot" : "inline") + "-sections.txt");
        Assert.Equal(File.ReadAllText(baseline).Replace("\r\n", "\n"), text.ToString());
    }

    private static string TestDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") =>
        Path.GetDirectoryName(path)!;

    private static string Normalize(string content) =>
        Regex.Replace(
            Regex.Replace(content.Replace("\r\n", "\n"),
                @"^(\* (Current working directory|Git repository root|Git repository|Session folder|Available tools)):.*$", "$1: <dynamic>",
                RegexOptions.Multiline),
            @"^CRITICAL: Since you're running on .*$", "<os-path-note>", RegexOptions.Multiline);

}
