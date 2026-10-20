// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Configuration;
using Core.Configuration.Abstractions;
using Core.Runtime;
using Cli.Tests.Recovery;
using Microsoft.Extensions.DependencyInjection;

namespace Cli.Tests;

[Collection("Azclips Service Provider Tests")]
public sealed class RecoveryConfigurationTests
{
    [Theory]
    [InlineData("trigger,style,size")]
    [InlineData("trigger,size,style")]
    [InlineData("style,trigger,size")]
    [InlineData("style,size,trigger")]
    [InlineData("size,trigger,style")]
    [InlineData("size,style,trigger")]
    public async Task AllSettingOrders_RoundTripPublicKeysAndCanonicalStorage(string order)
    {
        using var scope = new FailureHistoryTestScope();
        var values = new Dictionary<string, (string Key, object Value)>
        {
            ["trigger"] = ("agent.recovery", "auto"),
            ["style"] = ("agent.recovery.style", "interactive"),
            ["size"] = ("agent.recovery.historySize", 3),
        };
        foreach (var name in order.Split(','))
            await scope.Configuration.SetConfigurationAsync(values[name].Key, values[name].Value);

        var fresh = scope.CreateConfiguration();
        var options = fresh.GetConfiguration<AgentOptions>("agent").Recovery;
        Assert.Equal("auto", options.Trigger);
        Assert.Equal("interactive", options.Style);
        Assert.Equal(3, options.HistorySize);
        var document = await scope.Provider.ReadAsync();
        Assert.Equal("auto", document["agent"]!["recovery"]!["trigger"]!.GetValue<string>());
        var publicValues = await fresh.ListConfigurationWithSourcesAsync();
        Assert.Equal("global config", publicValues["agent.recovery"].Source);
        Assert.DoesNotContain("agent.recovery.trigger", publicValues.Keys);
        Assert.Equal(3, publicValues.Keys.Count(key => key.StartsWith("agent.recovery", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("agent.recovery", "off", "interactive", 3)]
    [InlineData("agent.recovery.style", "auto", "oneshot", 3)]
    [InlineData("agent.recovery.historySize", "auto", "interactive", 10)]
    public async Task Delete_RestoresOnlyTheRequestedDefault(string key, string trigger, string style, int size)
    {
        using var scope = new FailureHistoryTestScope();
        await scope.Configuration.SetConfigurationAsync("agent.recovery", "auto");
        await scope.Configuration.SetConfigurationAsync("agent.recovery.style", "interactive");
        await scope.Configuration.SetConfigurationAsync("agent.recovery.historySize", 3);
        await scope.Configuration.DeleteConfigurationAsync(key);
        var options = scope.CreateConfiguration().GetConfiguration<AgentOptions>().Recovery;
        Assert.Equal((trigger, style, size), (options.Trigger, options.Style, options.HistorySize));
        Assert.Equal("default", (await scope.Configuration.ListConfigurationWithSourcesAsync())[key].Source);
    }

    [Theory]
    [InlineData("""{"agent":{"enabled":true,"sessionStyle":"interactive","provider":"github"}}""")]
    [InlineData("""{"agent":{"enabled":false},"agent.sessionStyle":"interactive"}""")]
    [InlineData("""{"agent:enabled":"true"}""")]
    public async Task RetiredKeys_AreRemovedOnFirstReadAndNotMigrated(string stored)
    {
        using var scope = new FailureHistoryTestScope(trigger: null);
        await scope.Provider.WriteAsync(JsonNode.Parse(stored)!.AsObject());

        var recovery = scope.Configuration.GetConfiguration<AgentOptions>().Recovery;

        Assert.Equal(("off", "oneshot", 10), (recovery.Trigger, recovery.Style, recovery.HistorySize));
        var remaining = await scope.Configuration.ListConfigurationAsync();
        Assert.DoesNotContain(remaining.Keys, key => key.Contains("enabled", StringComparison.OrdinalIgnoreCase) && key.StartsWith("agent", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("sessionStyle", remaining.Keys, StringComparer.OrdinalIgnoreCase);
        if (stored.Contains("github", StringComparison.Ordinal))
            Assert.Equal("github", remaining["agent.provider"]);
    }

    [Fact]
    public async Task RetiredKeyRemoval_WritesOnceAndCleanConfigurationIsNeverRewritten()
    {
        var writes = 0;
        using var scope = new FailureHistoryTestScope();
        var provider = new CountingProvider(scope.Provider, () => writes++);
        var configuration = scope.CreateConfiguration(provider);
        await scope.Provider.WriteAsync(JsonNode.Parse("""{"agent":{"enabled":true}}""")!.AsObject());

        for (var index = 0; index < 3; index++)
            _ = configuration.GetConfiguration<AgentOptions>();

        Assert.Equal(1, writes);
    }

    [Theory]
    [InlineData("agent.enabled")]
    [InlineData("agent.sessionStyle")]
    public async Task RetiredKeys_CannotBeWrittenAgainEvenWithForce(string key)
    {
        using var scope = new FailureHistoryTestScope();
        var error = await Assert.ThrowsAsync<InvalidInputException>(() => scope.Configuration.SetConfigurationAsync(key, true, force: true));
        Assert.Contains("agent.recovery", error.Message);
        Assert.DoesNotContain(key, File.ReadAllText(scope.Provider.GetConfigFilePath()), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CountingProvider(Core.Configuration.Abstractions.IConfigurationProvider inner, Action onWrite)
        : Core.Configuration.Abstractions.IConfigurationProvider
    {
        public string GetConfigFilePath() => inner.GetConfigFilePath();
        public Task<JsonObject> ReadAsync(CancellationToken cancellationToken = default) => inner.ReadAsync(cancellationToken);
        public Task WriteAsync(JsonObject configuration, CancellationToken cancellationToken = default)
        {
            onWrite();
            return inner.WriteAsync(configuration, cancellationToken);
        }
    }

    [Theory]
    [InlineData("agent.recovery", "on-demand")]
    [InlineData("agent.recovery.style", "auto")]
    [InlineData("agent.recovery.historySize", 0)]
    [InlineData("agent.recovery.historySize", -1)]
    [InlineData("agent.recovery.historySize", 1.5)]
    [InlineData("agent.recovery.historySize", "2147483648")]
    [InlineData("agent.recovery", null)]
    [InlineData("agent.recovery.style", null)]
    [InlineData("agent.recovery.historySize", null)]
    [InlineData("agent.recovery.trigger", "auto")]
    [InlineData("agent.recovery.unknown", "auto")]
    public async Task InvalidWrites_AreNotSilentlyDefaulted(string key, object? value)
    {
        using var scope = new FailureHistoryTestScope();
        await Assert.ThrowsAsync<InvalidInputException>(() => scope.Configuration.SetConfigurationAsync(key, value, force: true));
        Assert.False(Directory.Exists(scope.DirectoryPath));
    }

    [Theory]
    [InlineData("\"auto\"")]
    [InlineData("null")]
    [InlineData("false")]
    [InlineData("[]")]
    public async Task ScalarRecovery_ReportsShapeErrorAndIsNeverOverwritten(string value)
    {
        using var scope = new FailureHistoryTestScope();
        await scope.Provider.WriteAsync(JsonNode.Parse($"{{\"agent\":{{\"recovery\":{value}}}}}")!.AsObject());
        var original = await File.ReadAllBytesAsync(scope.Provider.GetConfigFilePath());
        var error = Assert.Throws<InvalidOperationException>(() => scope.Configuration.GetConfiguration<AgentOptions>());
        Assert.Contains(scope.Provider.GetConfigFilePath(), error.Message);
        Assert.Contains("Edit", error.Message);
        Assert.Contains("replace or remove", error.Message);
        Assert.DoesNotContain("az config set", error.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Configuration.SetConfigurationAsync("agent.recovery.style", "interactive"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Configuration.DeleteConfigurationAsync("agent.recovery"));
        Assert.Equal(original, await File.ReadAllBytesAsync(scope.Provider.GetConfigFilePath()));
    }

    [Theory]
    [InlineData(true, "warn")]
    [InlineData(false, "error")]
    public async Task NonRecoveryFlatAndNestedKeys_KeepExistingReadAndRepairBehavior(bool flatFirst, string listedValue)
    {
        using var scope = new FailureHistoryTestScope(trigger: null);
        var flat = new KeyValuePair<string, JsonNode?>("core.logging.level", JsonValue.Create("error"));
        var nested = new KeyValuePair<string, JsonNode?>("core", JsonNode.Parse("""{"logging":{"level":"warn"}}"""));
        await scope.Provider.WriteAsync(new JsonObject(flatFirst ? [flat, nested] : [nested, flat]));

        Assert.Equal("warn", scope.Configuration.GetConfiguration<CoreOptions>().Logging.Level);
        Assert.Equal(listedValue, (await scope.Configuration.ListConfigurationAsync())["core.logging.level"]);
        Assert.Equal("off", scope.Configuration.GetConfiguration<AgentOptions>().Recovery.Trigger);

        await scope.Configuration.SetConfigurationAsync("core.logging.level", "info");
        var repaired = await scope.Provider.ReadAsync();
        Assert.False(repaired.ContainsKey("core.logging.level"));
        Assert.Equal("info", scope.Configuration.GetConfiguration<CoreOptions>().Logging.Level);
        await scope.Configuration.DeleteConfigurationAsync("core.logging.level");
        Assert.DoesNotContain("core.logging.level", (await scope.Configuration.ListConfigurationAsync()).Keys);
    }

    [Theory]
    [InlineData("trigger", "\"unknown\"")]
    [InlineData("style", "\"auto\"")]
    [InlineData("historySize", "0")]
    [InlineData("historySize", "2.5")]
    [InlineData("historySize", "2147483648")]
    [InlineData("style", "null")]
    [InlineData("unknown", "\"value\"")]
    public async Task InvalidFileValues_FailTypedAndSourceAwareReads(string leaf, string value)
    {
        using var scope = new FailureHistoryTestScope();
        await scope.Provider.WriteAsync(JsonNode.Parse($"{{\"agent\":{{\"recovery\":{{\"{leaf}\":{value}}}}}}}")!.AsObject());
        Assert.Throws<ArgumentException>(() => scope.Configuration.GetConfiguration<AgentOptions>());
        await Assert.ThrowsAsync<ArgumentException>(() => scope.Configuration.ListConfigurationWithSourcesAsync());
    }

    [Theory]
    [InlineData("""{"agent":{"recovery":{"trigger":"manual","TRIGGER":"auto"}}}""")]
    [InlineData("""{"agent":{"recovery":{}},"AGENT":{"recovery":"auto"}}""")]
    [InlineData("""{"agent.recovery.trigger":"auto"}""")]
    [InlineData("""{"agent":{"recovery.trigger":"auto"}}""")]
    public async Task AmbiguousOrNonobjectFiles_NeverBecomeSilentDefaults(string json)
    {
        using var scope = new FailureHistoryTestScope();
        Directory.CreateDirectory(scope.Root);
        await File.WriteAllTextAsync(scope.Provider.GetConfigFilePath(), json);
        Assert.Throws<InvalidOperationException>(() => scope.Configuration.GetConfiguration<AgentOptions>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Configuration.ListConfigurationWithSourcesAsync());
    }

    [Theory]
    [InlineData("AZCLIPS_AGENT_RECOVERY", "agent.recovery", "auto", true)]
    [InlineData("AZCLIPS_AGENT_RECOVERY_STYLE", "agent.recovery.style", "interactive", true)]
    [InlineData("AZCLIPS_AGENT_RECOVERY_HISTORY_SIZE", "agent.recovery.historySize", "4", true)]
    [InlineData("AZCLIPS_AGENT_RECOVERY", "agent.recovery", " ", false)]
    [InlineData("AZCLIPS_AGENT_RECOVERY", "agent.recovery", "oneshot", false)]
    [InlineData("AZCLIPS_AGENT_RECOVERY_STYLE", "agent.recovery.style", "manual", false)]
    [InlineData("AZCLIPS_AGENT_RECOVERY_HISTORY_SIZE", "agent.recovery.historySize", "0", false)]
    [InlineData("AZCLIPS_AGENT_RECOVERY_HISTORY_SIZE", "agent.recovery.historySize", "2147483648", false)]
    public async Task Environment_UsesThePublicMappingAndValidation(string variable, string key, string value, bool valid)
    {
        using var scope = new FailureHistoryTestScope();
        var original = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, value);
        try
        {
            if (!valid)
            {
                Assert.Throws<ArgumentException>(() => scope.Configuration.GetConfiguration<AgentOptions>());
                await Assert.ThrowsAsync<ArgumentException>(() => scope.Configuration.ListConfigurationWithSourcesAsync());
                return;
            }
            var options = scope.Configuration.GetConfiguration<AgentOptions>().Recovery;
            var sourced = await scope.Configuration.ListConfigurationWithSourcesAsync();
            Assert.Equal("environment", sourced[key].Source);
            Assert.Equal(value, Convert.ToString(sourced[key].Value));
            Assert.Equal(key switch
            {
                "agent.recovery" => options.Trigger,
                "agent.recovery.style" => options.Style,
                _ => options.HistorySize.ToString(),
            }, value);
            Assert.DoesNotContain("agent.recovery.trigger", sourced.Keys);
            Assert.False(Directory.Exists(scope.DirectoryPath));
        }
        finally { Environment.SetEnvironmentVariable(variable, original); }
    }

    [Fact]
    public async Task SharedRegistration_HasNoCycleAndFreshPolicyRejectsStaleOptions()
    {
        using var scope = new FailureHistoryTestScope();
        var keys = new TestFailureHistoryKeys();
        var services = new ServiceCollection();
        services.RegisterConfigurationServices().RegisterRuntimeServices();
        services.AddSingleton<IConfigurationProvider>(scope.Provider);
        services.AddSingleton(scope.State);
        services.AddSingleton<IFailureHistoryKeyStore>(keys);
        using var provider = services.BuildServiceProvider();
        var config = provider.GetRequiredService<IConfigurationService>();
        var store = provider.GetRequiredService<IFailureHistoryStore>();
        var oldOptions = config.GetConfiguration<AgentOptions>();
        await store.SaveAsync(FailureHistoryTestScope.CreateRecord(), 10);
        await config.SetConfigurationAsync("agent.recovery", "off");
        Assert.True(oldOptions.Recovery.IsEnabled);
        Assert.False(config.GetConfiguration<AgentOptions>().Recovery.IsEnabled);
        Assert.Equal("failure_history_disabled",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(FailureHistoryTestScope.CreateRecord(), 10))).Code);
        Assert.Empty(await store.ReadIndexAsync());
        Assert.Single(keys.CreateRequests);
    }
}
