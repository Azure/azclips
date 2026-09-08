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
    [InlineData("error", 0)]
    [InlineData("warn", 1)]
    [InlineData("info", 2)]
    [InlineData("debug", 3)]
    [InlineData("DEBUG", 3)]
    [InlineData(null, 1)]
    [InlineData("", 1)]
    [InlineData("invalid", 1)]
    public void Parse_ShouldReturnExpectedLevel(string? configuredValue, int expected)
    {
        var level = CliLoggingPolicy.Parse(configuredValue);

        Assert.Equal((CliLogLevel)expected, level);
    }

    [Fact]
    public void ShouldEmit_ShouldApplyExpectedMatrix()
    {
        Assert.True(CliLoggingPolicy.ShouldEmit(CliLogLevel.Error, CliLogSeverity.Error));
        Assert.False(CliLoggingPolicy.ShouldEmit(CliLogLevel.Error, CliLogSeverity.Warn));
        Assert.False(CliLoggingPolicy.ShouldEmit(CliLogLevel.Error, CliLogSeverity.Info));
        Assert.False(CliLoggingPolicy.ShouldEmit(CliLogLevel.Error, CliLogSeverity.Debug));
        Assert.False(CliLoggingPolicy.ShouldEmit(CliLogLevel.Error, CliLogSeverity.Verbose));

        Assert.True(CliLoggingPolicy.ShouldEmit(CliLogLevel.Warn, CliLogSeverity.Error));
        Assert.True(CliLoggingPolicy.ShouldEmit(CliLogLevel.Warn, CliLogSeverity.Warn));
        Assert.False(CliLoggingPolicy.ShouldEmit(CliLogLevel.Warn, CliLogSeverity.Info));
        Assert.False(CliLoggingPolicy.ShouldEmit(CliLogLevel.Warn, CliLogSeverity.Debug));
        Assert.False(CliLoggingPolicy.ShouldEmit(CliLogLevel.Warn, CliLogSeverity.Verbose));

        Assert.True(CliLoggingPolicy.ShouldEmit(CliLogLevel.Info, CliLogSeverity.Error));
        Assert.True(CliLoggingPolicy.ShouldEmit(CliLogLevel.Info, CliLogSeverity.Warn));
        Assert.True(CliLoggingPolicy.ShouldEmit(CliLogLevel.Info, CliLogSeverity.Info));
        Assert.False(CliLoggingPolicy.ShouldEmit(CliLogLevel.Info, CliLogSeverity.Debug));
        Assert.False(CliLoggingPolicy.ShouldEmit(CliLogLevel.Info, CliLogSeverity.Verbose));

        Assert.True(CliLoggingPolicy.ShouldEmit(CliLogLevel.Debug, CliLogSeverity.Error));
        Assert.True(CliLoggingPolicy.ShouldEmit(CliLogLevel.Debug, CliLogSeverity.Warn));
        Assert.True(CliLoggingPolicy.ShouldEmit(CliLogLevel.Debug, CliLogSeverity.Info));
        Assert.True(CliLoggingPolicy.ShouldEmit(CliLogLevel.Debug, CliLogSeverity.Debug));
        Assert.True(CliLoggingPolicy.ShouldEmit(CliLogLevel.Debug, CliLogSeverity.Verbose));
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

            Assert.Equal(CliLogLevel.Error, level);
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

        Assert.Equal(CliLogLevel.Debug, level);
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
