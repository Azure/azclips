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
    [InlineData("off", false, false)]
    [InlineData("manual", true, false)]
    [InlineData("AUTO", true, true)]
    public async Task Recovery_IsASingleValueKey(string value, bool enabled, bool automatic)
    {
        using var scope = new FailureHistoryTestScope(trigger: null);
        await scope.Configuration.SetConfigurationAsync("agent.recovery", value);
        var options = scope.Configuration.GetConfiguration<AgentOptions>();
        Assert.Equal(enabled, options.IsRecoveryEnabled);
        Assert.Equal(automatic, options.IsRecoveryAutomatic);
        Assert.Equal(value, (await scope.Provider.ReadAsync())["agent"]!["recovery"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("on-demand")]
    [InlineData("true")]
    public async Task Recovery_RejectsUnknownValues(string value)
    {
        using var scope = new FailureHistoryTestScope(trigger: null);
        await Assert.ThrowsAsync<InvalidInputException>(() => scope.Configuration.SetConfigurationAsync("agent.recovery", value));
        Assert.False(Directory.Exists(scope.DirectoryPath));
    }

    [Fact]
    public async Task HandEditedUnknownRecovery_FailsTheRecoveryCheckWithoutBreakingOtherReads()
    {
        using var scope = new FailureHistoryTestScope(trigger: null);
        await scope.Provider.WriteAsync(JsonNode.Parse("""{"agent":{"recovery":"sometimes","provider":"github"}}""")!.AsObject());
        var options = scope.Configuration.GetConfiguration<AgentOptions>();
        Assert.Equal("github", options.Provider);
        Assert.Throws<ArgumentException>(() => options.IsRecoveryEnabled);
    }

    [Fact]
    public async Task RetiredAgentEnabled_IsIgnoredAndLeftInPlace()
    {
        using var scope = new FailureHistoryTestScope(trigger: null);
        await scope.Provider.WriteAsync(JsonNode.Parse("""{"agent":{"enabled":true,"sessionStyle":"interactive"}}""")!.AsObject());
        var before = await File.ReadAllBytesAsync(scope.Provider.GetConfigFilePath());
        var options = scope.Configuration.GetConfiguration<AgentOptions>();
        Assert.False(options.IsRecoveryEnabled);
        Assert.Equal("interactive", options.SessionStyle);
        Assert.Equal(before, await File.ReadAllBytesAsync(scope.Provider.GetConfigFilePath()));
        await Assert.ThrowsAsync<InvalidInputException>(() => scope.Configuration.SetConfigurationAsync("agent.enabled", true));
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
        Assert.True(oldOptions.IsRecoveryEnabled);
        Assert.False(config.GetConfiguration<AgentOptions>().IsRecoveryEnabled);
        Assert.Equal("failure_history_disabled",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(FailureHistoryTestScope.CreateRecord(), 10))).Code);
        Assert.Empty(await store.ReadIndexAsync());
        Assert.Single(keys.CreateRequests);
    }
}
