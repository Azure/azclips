// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using Core.Cli.Recovery;
using Core.Configuration.Abstractions;
using Core.Configuration.Implementation;
using Core.Configuration.Schema;

namespace Cli.Tests.Recovery;

public sealed class RecoveryOnboardingTests : IDisposable
{
    private readonly string _root = Path.GetFullPath(Path.Combine(".test-artifacts", "recovery-onboarding", Guid.NewGuid().ToString("N")));
    private readonly PathConfigurationProvider _provider;
    private readonly ConfigurationService _configuration;

    public RecoveryOnboardingTests()
    {
        _provider = new PathConfigurationProvider(Path.Combine(_root, "config.json"));
        var schema = new ConfigurationSchema();
        _configuration = new ConfigurationService(_provider, schema, new DefaultConfigurationValidator(schema));
    }

    private string StatePath => Path.Combine(_root, "agent", "state.json");

    [Fact]
    public async Task Hint_IsShownFifteenTimesWhileRecoveryIsUnset()
    {
        var shown = 0;
        for (var index = 0; index < RecoveryOnboarding.MaxHints + 3; index++)
        {
            var error = new StringWriter();
            await RecoveryOnboarding.RunAsync(_configuration, error, StatePath);
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
        await _configuration.SetConfigurationAsync("agent.recovery.trigger", trigger);
        var error = new StringWriter();
        await RecoveryOnboarding.RunAsync(_configuration, error, StatePath);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task Hint_LeavesRetiredKeysForAgentStart()
    {
        await _provider.WriteAsync(JsonNode.Parse("""{"agent":{"enabled":true}}""")!.AsObject());
        await RecoveryOnboarding.RunAsync(_configuration, TextWriter.Null, StatePath);
        Assert.Equal(true, (await _configuration.ListConfigurationAsync())["agent.enabled"]);
    }

    [Fact]
    public async Task LostStateFile_OnlyRepeatsTheHint()
    {
        await RecoveryOnboarding.RunAsync(_configuration, TextWriter.Null, StatePath);
        File.Delete(StatePath);
        var error = new StringWriter();
        await RecoveryOnboarding.RunAsync(_configuration, error, StatePath);
        Assert.Contains(RecoveryOnboarding.ManualHint, error.ToString());
    }

    [Fact]
    public async Task TryRun_DoesNotThrowWhenConfigurationIsMalformed()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(_provider.GetConfigFilePath(), "{ broken");
        await RecoveryOnboarding.TryRunAsync(_configuration, TextWriter.Null, StatePath);
    }

    [Fact]
    public async Task StateFile_IsVersionedAndKeepsOtherSections()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        File.WriteAllText(StatePath, """{"version":1,"other":{"keep":true}}""");

        await RecoveryOnboarding.RunAsync(_configuration, TextWriter.Null, StatePath);

        var document = JsonNode.Parse(File.ReadAllText(StatePath))!.AsObject();
        Assert.Equal(1, (int)document["version"]!);
        Assert.True((bool)document["other"]!["keep"]!);
        Assert.Equal(1, (int)document["recovery"]!["hintsShown"]!);
        Assert.Null(document["recovery"]!["retiredKeysRemoved"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}

internal sealed class PathConfigurationProvider(string configFilePath) : IConfigurationProvider
{
    public string GetConfigFilePath() => configFilePath;

    public async Task<JsonObject> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(configFilePath))
            return [];
        var json = await File.ReadAllTextAsync(configFilePath, cancellationToken);
        return string.IsNullOrWhiteSpace(json) ? [] : JsonNode.Parse(json) as JsonObject ?? [];
    }

    public async Task WriteAsync(JsonObject configuration, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(configFilePath)!);
        var temp = configFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(temp, configuration.ToJsonString(), cancellationToken);
        File.Move(temp, configFilePath, overwrite: true);
    }
}
