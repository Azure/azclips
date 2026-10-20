// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using Core.Cli.Recovery;

namespace Cli.Tests.Recovery;

public sealed class RecoveryOnboardingTests
{
    private static string StatePath(FailureHistoryTestScope scope) => Path.Combine(scope.Root, "agent", "state.json");

    [Fact]
    public async Task RetiredKeys_AreDeletedOnceAndOtherKeysAreKept()
    {
        using var scope = new FailureHistoryTestScope(trigger: null);
        await scope.Provider.WriteAsync(JsonNode.Parse(
            """{"agent":{"enabled":true,"sessionStyle":"interactive","provider":"github","builtin":{"endpoint":"https://example.test/","modelId":"m"}}}""")!.AsObject());

        await RecoveryOnboarding.RunAsync(scope.Configuration, TextWriter.Null, StatePath(scope));

        var listed = await scope.Configuration.ListConfigurationAsync();
        Assert.DoesNotContain("agent.enabled", listed.Keys);
        Assert.DoesNotContain("agent.sessionStyle", listed.Keys);
        Assert.DoesNotContain("agent.builtin.endpoint", listed.Keys);
        Assert.Equal("github", listed["agent.provider"]);
        Assert.Equal("m", listed["agent.builtin.modelId"]);

        await scope.Provider.WriteAsync(JsonNode.Parse("""{"agent":{"enabled":true}}""")!.AsObject());
        await RecoveryOnboarding.RunAsync(scope.Configuration, TextWriter.Null, StatePath(scope));
        Assert.Equal(true, (await scope.Configuration.ListConfigurationAsync())["agent.enabled"]);
    }

    [Fact]
    public async Task Hint_IsShownFifteenTimesWhileRecoveryIsUnset()
    {
        using var scope = new FailureHistoryTestScope(trigger: null);
        var shown = 0;
        for (var index = 0; index < RecoveryOnboarding.MaxHints + 3; index++)
        {
            var error = new StringWriter();
            await RecoveryOnboarding.RunAsync(scope.Configuration, error, StatePath(scope));
            if (error.ToString().Length > 0)
            {
                Assert.Equal(RecoveryOnboarding.ManualHint + Environment.NewLine + RecoveryOnboarding.AutoHint + Environment.NewLine, error.ToString());
                shown++;
            }
        }
        Assert.Equal(RecoveryOnboarding.MaxHints, shown);
    }

    [Theory]
    [InlineData("off")]
    [InlineData("manual")]
    [InlineData("auto")]
    public async Task Hint_IsNotShownOnceRecoveryIsSet(string trigger)
    {
        using var scope = new FailureHistoryTestScope(trigger: trigger);
        var error = new StringWriter();
        await RecoveryOnboarding.RunAsync(scope.Configuration, error, StatePath(scope));
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task LostStateFile_OnlyRepeatsTheWork()
    {
        using var scope = new FailureHistoryTestScope(trigger: null);
        await RecoveryOnboarding.RunAsync(scope.Configuration, TextWriter.Null, StatePath(scope));
        File.Delete(StatePath(scope));
        var error = new StringWriter();
        await RecoveryOnboarding.RunAsync(scope.Configuration, error, StatePath(scope));
        Assert.Contains(RecoveryOnboarding.ManualHint, error.ToString());
    }

    [Fact]
    public async Task TryRun_DoesNotThrowWhenConfigurationIsMalformed()
    {
        using var scope = new FailureHistoryTestScope(trigger: null);
        await scope.Provider.WriteAsync(JsonNode.Parse("""{"agent":{"recovery":"auto"}}""")!.AsObject());
        await RecoveryOnboarding.TryRunAsync(scope.Configuration, TextWriter.Null, StatePath(scope));
    }

    [Fact]
    public async Task StateFile_IsVersionedAndKeepsOtherSections()
    {
        using var scope = new FailureHistoryTestScope(trigger: null);
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath(scope))!);
        File.WriteAllText(StatePath(scope), """{"version":1,"other":{"keep":true}}""");

        await RecoveryOnboarding.RunAsync(scope.Configuration, TextWriter.Null, StatePath(scope));

        var document = JsonNode.Parse(File.ReadAllText(StatePath(scope)))!.AsObject();
        Assert.Equal(1, (int)document["version"]!);
        Assert.True((bool)document["other"]!["keep"]!);
        Assert.True((bool)document["recovery"]!["retiredKeysRemoved"]!);
        Assert.Equal(1, (int)document["recovery"]!["hintsShown"]!);
    }
}
