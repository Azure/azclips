// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Configuration.Abstractions;
using Core.Configuration.Implementation;
using Core.Configuration.Options;
using Core.Configuration.Schema;
using Core.Runtime.Recovery;

namespace Cli.Tests;

[Collection("Azclips Service Provider Tests")]
public class ConfigurationServiceTests
{
    [Fact]
    public async Task SetConfigurationAsync_ShouldRoundTripTypedOptions()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        await service.SetConfigurationAsync("core.logging.level", "debug");

        var options = service.GetConfiguration<CoreOptions>();

        Assert.Equal("debug", options.Logging.Level);
    }

    [Fact]
    public async Task TypedBinding_PreservesExistingArrayAndDictionarySemantics()
    {
        using var fixture = new TempConfigurationFixture();
        await File.WriteAllTextAsync(fixture.Provider.GetConfigFilePath(),
            """{"custom":{"items":["one","two"],"labels":{"name":"example"}}}""");
        var options = fixture.CreateService().GetConfiguration<CollectionOptions>("custom");
        Assert.Equal(new[] { "one", "two" }, options.Items);
        Assert.Equal("example", options.Labels["name"]);
    }

    [Fact]
    public async Task SetConfigurationAsync_ShouldRoundTripPowerShellOutputFormat()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        Assert.Equal("object", service.GetConfiguration<ClientsOptions>().PowerShell.Output.Format);

        await service.SetConfigurationAsync("clients.powershell.output.format", "json");

        Assert.Equal("json", service.GetConfiguration<ClientsOptions>().PowerShell.Output.Format);
    }

    [Fact]
    public async Task DeleteConfigurationAsync_ShouldPruneEmptyParents()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        await service.SetConfigurationAsync("core.logging.level", "debug");
        await service.DeleteConfigurationAsync("core.logging.level");

        var values = await service.ListConfigurationAsync();
        var json = await fixture.Provider.ReadAsync();

        Assert.DoesNotContain("core.logging.level", values.Keys);
        Assert.False(json.ContainsKey("core"));
    }

    [Fact]
    public async Task GetConfiguration_ShouldPreferEnvironmentOverrides()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();
        await service.SetConfigurationAsync("core.logging.level", "info");

        Environment.SetEnvironmentVariable("AZCLIPS_LOG_LEVEL", "error");

        try
        {
            var options = service.GetConfiguration<CoreOptions>();

            Assert.Equal("error", options.Logging.Level);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZCLIPS_LOG_LEVEL", null);
        }
    }

    [Fact]
    public async Task GetConfiguration_ShouldAcceptColonDelimitedKey()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();
        await service.SetConfigurationAsync("core.logging.level", "warn");

        var exception = Record.Exception(() => service.GetConfiguration<CoreOptions>("core:logging:level"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task SetConfigurationAsync_ShouldUseSchemaCanonicalKeyCasing_ForKnownKeys()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        await service.SetConfigurationAsync("core.http.maxretriesfor429", 7);

        var values = await service.ListConfigurationAsync();

        Assert.Contains("core.http.maxRetriesFor429", values.Keys);
    }

    [Fact]
    public async Task GetConfiguration_ShouldThrowClearError_WhenConfigFileContainsInvalidJson()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        await File.WriteAllTextAsync(fixture.Provider.GetConfigFilePath(), "{ invalid json }");

        var exception = Assert.Throws<InvalidInputException>(() => service.GetConfiguration<CoreOptions>());

        Assert.Contains(fixture.Provider.GetConfigFilePath(), exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("invalid json", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListConfigurationAsync_ShouldIncludeDefaults_WhenRequested()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        var values = await service.ListConfigurationAsync(includeDefaults: true);

        Assert.Equal(512, values["core.engine.extensionIndexCacheSize"]);
        Assert.Equal(3, values["core.http.maxRetries"]);
    }

    [Fact]
    public async Task ListConfigurationWithSourcesAsync_ShouldReturnOneSourceAwareSnapshot()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        await service.SetConfigurationAsync("core.logging.level", "debug");
        var values = await service.ListConfigurationWithSourcesAsync();

        Assert.Equal("debug", values["core.logging.level"].Value);
        Assert.Equal("global config", values["core.logging.level"].Source);
        Assert.Equal("oneshot", values["agent.recovery.style"].Value);
        Assert.Equal("default", values["agent.recovery.style"].Source);
    }

    [Fact]
    public async Task ListConfigurationAsync_ShouldExposeExactlyTheSchemasAgentKeys()
    {
        // Both schema and defaults now traverse configuration attributes. Their agent key sets
        // must agree, and the shared ARM scope must remain non-configurable.
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        var values = await service.ListConfigurationAsync(includeDefaults: true);

        var exposed = values.Keys
            .Where(k => k.StartsWith("agent.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
        var schema = new ConfigurationSchema().GetAllKeys()
            .Select(e => e.Key)
            .Where(k => k.StartsWith("agent.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(schema, exposed, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("agent.builtin.scope", exposed, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveDefaultParameterAsync_ShouldReturnDefaultsValue_WhenConfigured()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        await service.SetConfigurationAsync("defaults.group", "my-rg");

        var value = await service.ResolveDefaultParameterAsync("group");

        Assert.Equal("my-rg", value);
    }


    [Fact]
    public void GetConfiguration_ShouldDefaultRecoveryOff_OnEmptyConfig()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        var options = service.GetConfiguration<AgentOptions>();

        Assert.False(options.Recovery.IsEnabled);
        Assert.False(options.Recovery.IsAutomatic);
        Assert.Equal("off", options.Recovery.Trigger);
        Assert.Equal("oneshot", options.Recovery.Style);
        Assert.Equal("builtin", options.Provider);
        Assert.Equal("gpt-5.4-mini", options.Builtin.ModelId);
    }

    [Fact]
    public async Task GetConfiguration_ShouldUseConfiguredBuiltinModel_InsteadOfDefault()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();
        await service.SetConfigurationAsync("agent.builtin.modelId", "custom-deployment");

        var options = service.GetConfiguration<AgentOptions>();

        Assert.Equal("custom-deployment", options.Builtin.ModelId);
        Assert.Equal("custom-deployment", Core.Telemetry.RecoveryTelemetry.GetConfiguredModel(options));
    }

    [Fact]
    public void GetConfiguration_ShouldDefaultBuiltinModelToMini_OnEmptyConfig()
    {
        using var fixture = new TempConfigurationFixture();
        var options = fixture.CreateService().GetConfiguration<AgentOptions>();

        Assert.Equal("builtin", options.Provider);
        Assert.Equal("gpt-5.4-mini", options.Builtin.ModelId);
    }

    [Theory]
    [InlineData("gpt-5.4")]
    [InlineData("custom-deployment")]
    public async Task GetConfiguration_ShouldPreserveExplicitBuiltinModel(string model)
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        await service.SetConfigurationAsync("agent.builtin.modelId", model);

        Assert.Equal(model, service.GetConfiguration<AgentOptions>().Builtin.ModelId);
        var values = await service.ListConfigurationAsync(includeDefaults: true);
        Assert.Equal(model, values["agent.builtin.modelId"]);
    }

    [Fact]
    public async Task DeleteConfigurationAsync_ShouldRestoreBuiltinModelDefault()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();
        await service.SetConfigurationAsync("agent.builtin.modelId", "gpt-5.4");

        await service.DeleteConfigurationAsync("agent.builtin.modelId");

        Assert.Equal("gpt-5.4-mini", service.GetConfiguration<AgentOptions>().Builtin.ModelId);
        var values = await service.ListConfigurationAsync(includeDefaults: true);
        Assert.Equal("gpt-5.4-mini", values["agent.builtin.modelId"]);
    }

    [Fact]
    public async Task GetConfiguration_ShouldPreserveGithubProviderSelection()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();
        await service.SetConfigurationAsync("agent.provider", "github");

        Assert.Equal("github", service.GetConfiguration<AgentOptions>().Provider);
        var values = await service.ListConfigurationAsync(includeDefaults: true);
        Assert.Equal("github", values["agent.provider"]);
    }

    [Fact]
    public async Task ListConfigurationAsync_ShouldReportBuiltinModelDefault()
    {
        using var fixture = new TempConfigurationFixture();
        var values = await fixture.CreateService().ListConfigurationAsync(includeDefaults: true);

        Assert.Equal("gpt-5.4-mini", values["agent.builtin.modelId"]);
    }

    [Fact]
    public void GetConfiguration_ShouldDefaultCompletionEnabled_OnEmptyConfig()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        var options = service.GetConfiguration<CompletionOptions>();

        // Completion is opt-out: sourcing a completion script works out of the box.
        Assert.True(options.Enabled);
    }

    [Fact]
    public async Task SetConfigurationAsync_ShouldRoundTripCompletionEnabled()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        await service.SetConfigurationAsync("completion.enabled", false);

        var options = service.GetConfiguration<CompletionOptions>();

        Assert.False(options.Enabled);
    }

    [Fact]
    public async Task ListConfigurationAsync_ShouldReportCompletionEnabledDefaultTrue()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        var values = await service.ListConfigurationAsync(includeDefaults: true);

        // Schema/list default must match the POCO default (CompletionOptions.Enabled=true).
        Assert.Equal(true, values["completion.enabled"]);
    }

    [Fact]
    public async Task ListConfigurationAsync_ShouldReportRecoveryOffDefault()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        var values = await service.ListConfigurationAsync(includeDefaults: true);

        Assert.Equal("off", values["agent.recovery"]);
        Assert.DoesNotContain("agent.enabled", values.Keys);
    }

    [Fact]
    public async Task AgentReasoningEffort_ShouldDefaultToMedium_InTypedOptionsAndListing()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        var options = service.GetConfiguration<AgentOptions>();
        var values = await service.ListConfigurationAsync(includeDefaults: true);
        var overrides = await service.ListConfigurationAsync();

        Assert.Equal("medium", options.ReasoningEffort);
        Assert.Equal("medium", values["agent.reasoningEffort"]);
        Assert.DoesNotContain("agent.reasoningEffort", overrides.Keys);
        Assert.Equal("builtin", options.Provider);
        Assert.Equal(new BuiltinProviderOptions().ModelId, options.Builtin.ModelId);
    }

    [Theory]
    [InlineData("low")]
    [InlineData("medium")]
    [InlineData("high")]
    public async Task AgentReasoningEffort_ShouldRoundTripAndResetToMedium(string effort)
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        await service.SetConfigurationAsync("agent.reasoningEffort", effort);

        Assert.Equal(effort, service.GetConfiguration<AgentOptions>().ReasoningEffort);
        Assert.Equal(effort, (await service.ListConfigurationAsync(includeDefaults: true))["agent.reasoningEffort"]);
        var json = await fixture.Provider.ReadAsync();
        Assert.Equal(effort, json["agent"]?["reasoningEffort"]?.GetValue<string>());

        await service.DeleteConfigurationAsync("agent.reasoningEffort");

        Assert.Equal("medium", service.GetConfiguration<AgentOptions>().ReasoningEffort);
        Assert.Equal("medium", (await service.ListConfigurationAsync(includeDefaults: true))["agent.reasoningEffort"]);
        Assert.DoesNotContain("agent.reasoningEffort", (await service.ListConfigurationAsync()).Keys);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("xhigh")]
    [InlineData("")]
    [InlineData(" medium ")]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(true)]
    public async Task AgentReasoningEffort_ShouldRejectInvalidOverrides_WithoutChangingConfiguration(object? effort)
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();
        await service.SetConfigurationAsync("agent.reasoningEffort", "high");

        var exception = await Assert.ThrowsAsync<InvalidInputException>(() =>
            service.SetConfigurationAsync("agent.reasoningEffort", effort));

        Assert.Contains("agent.reasoningEffort", exception.Message);
        Assert.Equal("high", service.GetConfiguration<AgentOptions>().ReasoningEffort);
    }


    private sealed class TempConfigurationFixture : IDisposable
    {
        private readonly string _directory = Path.GetFullPath(Path.Combine(".test-artifacts", "azclips-config-tests", Guid.NewGuid().ToString("N")));

        public TempConfigurationFixture()
        {
            Directory.CreateDirectory(_directory);
            Provider = new TestConfigurationProvider(Path.Combine(_directory, "config.json"));
        }

        public TestConfigurationProvider Provider { get; }

        public ConfigurationService CreateService()
        {
            var schema = new ConfigurationSchema();
            var validator = new DefaultConfigurationValidator(schema);
            return new ConfigurationService(Provider, schema, validator, new RecoveryConfigurationCoordinator(Path.Combine(_directory, "failure-history")));
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }

    private sealed class TestConfigurationProvider(string configFilePath) : IConfigurationProvider
    {
        public string GetConfigFilePath() => configFilePath;

        public async Task<JsonObject> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(configFilePath))
            {
                return [];
            }

            var json = await File.ReadAllTextAsync(configFilePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            return JsonNode.Parse(json) as JsonObject ?? [];
        }

        public async Task WriteAsync(JsonObject configuration, CancellationToken cancellationToken = default)
        {
            var directory = Path.GetDirectoryName(configFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(configFilePath, configuration.ToJsonString(new() { WriteIndented = true }), cancellationToken);
        }
    }

    private sealed class CollectionOptions
    {
        public string[] Items { get; set; } = [];
        public Dictionary<string, string> Labels { get; set; } = [];
    }
}
