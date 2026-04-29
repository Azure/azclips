using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Configuration.Abstractions;
using Core.Configuration.Implementation;
using Core.Configuration.Options;
using Core.Configuration.Schema;

namespace Cli.Tests;

[Collection("Cirrus Service Provider Tests")]
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

        Environment.SetEnvironmentVariable("CIRRUS_LOG_LEVEL", "error");

        try
        {
            var options = service.GetConfiguration<CoreOptions>();

            Assert.Equal("error", options.Logging.Level);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CIRRUS_LOG_LEVEL", null);
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
    public async Task GetConfiguration_ShouldThrowClearError_WhenConfigFileContainsInvalidJson()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        await File.WriteAllTextAsync(fixture.Provider.GetConfigFilePath(), "{ invalid json }");

        var exception = Assert.Throws<InvalidOperationException>(() => service.GetConfiguration<CoreOptions>());

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
        Assert.True(values.ContainsKey("assemblyLoading.builtinInstallationDir"));
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


    private sealed class TempConfigurationFixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "cirrus-config-tests", Guid.NewGuid().ToString("N"));

        public TempConfigurationFixture()
        {
            Directory.CreateDirectory(_directory);
            Provider = new TestConfigurationProvider(Path.Combine(_directory, "config.json"));
        }

        public TestConfigurationProvider Provider { get; }

        public IConfigurationService CreateService()
        {
            var schema = new ConfigurationSchema();
            var validator = new DefaultConfigurationValidator(schema);
            return new ConfigurationService(Provider, schema, validator);
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
}
