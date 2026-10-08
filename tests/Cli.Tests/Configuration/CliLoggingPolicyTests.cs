// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Cli;
using Core.Configuration.Abstractions;
using Core.Configuration.Implementation;
using Core.Configuration.Schema;

namespace Cli.Tests;

[Collection("Azclips Service Provider Tests")]
public class CliLoggingPolicyTests
{
    [Theory]
    [InlineData("error", LogLevel.Error)]
    [InlineData("warn", LogLevel.Warn)]
    [InlineData("info", LogLevel.Info)]
    [InlineData("debug", LogLevel.Debug)]
    [InlineData(" DEBUG ", LogLevel.Debug)]
    [InlineData("  ", LogLevel.Warn)]
    [InlineData("invalid", LogLevel.Warn)]
    public void ResolveEffectiveLevel_UsesSharedBaselineWithoutOverride(string configuredValue, LogLevel expected)
    {
        var options = new CoreOptions { Logging = new LoggingOptions { Level = configuredValue } };

        Assert.Equal(expected, CliLoggingPolicy.ResolveEffectiveLevel(options, debugOverride: false));
    }

    [Fact]
    public async Task ResolveEffectiveLevel_ShouldPreferEnvironmentOverGlobalConfig_WhenNoDebugOverride()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();
        await service.SetConfigurationAsync("core.logging.level", "warn");
        var originalLogLevel = Environment.GetEnvironmentVariable("AZCLIPS_LOG_LEVEL");
        Environment.SetEnvironmentVariable("AZCLIPS_LOG_LEVEL", "error");

        try
        {
            var level = CliLoggingPolicy.ResolveEffectiveLevel(service, debugOverride: false);

            Assert.Equal(LogLevel.Error, level);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZCLIPS_LOG_LEVEL", originalLogLevel);
        }
    }

    [Fact]
    public async Task ResolveEffectiveLevel_ShouldPreferDebugOverride_WhenPresent()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();
        await service.SetConfigurationAsync("core.logging.level", "error");

        var level = CliLoggingPolicy.ResolveEffectiveLevel(service, debugOverride: true);

        Assert.Equal(LogLevel.Debug, level);
    }

    private sealed class TempConfigurationFixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "azclips-cli-logging-policy-tests", Guid.NewGuid().ToString("N"));

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
