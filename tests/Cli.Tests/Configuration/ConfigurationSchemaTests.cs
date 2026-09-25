// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Configuration.Abstractions;
using Core.Configuration.Implementation;
using Core.Configuration.Schema;

namespace Cli.Tests;

public class ConfigurationSchemaTests
{
    private readonly IConfigurationSchema _schema = new ConfigurationSchema();

    [Fact]
    public void GetAllKeys_ShouldContainCoreLoggingLevel()
    {
        var keys = _schema.GetAllKeys();

        Assert.Contains(keys, k => string.Equals(k.Key, "core.logging.level", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetKeyMetadata_ShouldReturnCliSimpleOutputFormat_WithTsvDefault()
    {
        var key = _schema.GetKeyMetadata("clients.cli.output.simpleFormat");

        Assert.NotNull(key);
        Assert.Equal("tsv", key.DefaultValue);
        Assert.Equal(
            _schema.GetKeyMetadata("clients.cli.output.format")!.AllowedValues,
            key.AllowedValues);
    }

    [Fact]
    public void GetKeyMetadata_ShouldReturnAgentBuiltinScope_WithIntelligentApiDefault()
    {
        var key = _schema.GetKeyMetadata("agent.builtin.scope");

        Assert.NotNull(key);
        Assert.Equal("api://a5ede409-60d3-4a6c-93e6-eb2e7271e8e3/Azclis.Intelligent.All", key.DefaultValue);
    }

    [Theory]
    [InlineData("assemblyLoading.installedExtensionsRoot")]
    [InlineData("assemblyLoading.extensionFolderPrefix")]
    public void GetKeyMetadata_ShouldNotExposeDynamicRuntimeDefaults(string name)
    {
        var key = _schema.GetKeyMetadata(name);

        Assert.NotNull(key);
        Assert.Null(key.DefaultValue);
    }

    [Fact]
    public void GetAllKeys_ShouldNotExposeNestedExcludedSections()
    {
        var keys = _schema.GetAllKeys();

        Assert.DoesNotContain(keys, key => key.Key.StartsWith("benchmark.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetAllKeys_ShouldContainEveryCurrentConfigurationPath()
    {
        var expected = new[]
        {
            "agent.builtin.apiVersion", "agent.builtin.endpoint", "agent.builtin.modelId", "agent.builtin.scope",
            "agent.enabled", "agent.interactive.verbose", "agent.provider", "agent.sessionStyle", "agent.trace.enabled", "agent.trace.path",
            "assemblyLoading.bypassExtensionCache", "assemblyLoading.extensionFolderPrefix", "assemblyLoading.installedExtensionsRoot",
            "clients.cli.output.format", "clients.cli.output.simpleFormat", "clients.powershell.output.format", "completion.enabled",
            "core.engine.extensionIndexCacheSize", "core.http.maxRetries", "core.http.maxRetriesFor429", "core.logging.level", "core.output.color",
            "defaults.acr", "defaults.group", "defaults.location", "defaults.vm", "defaults.vmss", "defaults.web",
            "extensions.resources.arm.apiVersionOverride", "telemetry.enabled"
        };

        var actual = _schema.GetAllKeys().Select(key => key.Key).OrderBy(key => key, StringComparer.Ordinal).ToArray();

        Assert.Equal(expected.OrderBy(key => key, StringComparer.Ordinal), actual);
    }

    [Fact]
    public void IsKnownKey_ShouldReturnFalse_ForUnknownKey()
    {
        var known = _schema.IsKnownKey("core.notARealKey");

        Assert.False(known);
    }

    [Fact]
    public void ValidateKey_ShouldRejectUnknownKey_WhenForceIsFalse()
    {
        var validator = new DefaultConfigurationValidator(_schema);

        var result = validator.ValidateKey("core.notARealKey", force: false);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateKey_ShouldAllowUnknownKey_WhenForceIsTrue()
    {
        var validator = new DefaultConfigurationValidator(_schema);

        var result = validator.ValidateKey("core.notARealKey", force: true);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateValue_ShouldRejectInvalidAllowedValue()
    {
        var validator = new DefaultConfigurationValidator(_schema);

        var result = validator.ValidateValue("core.logging.level", "verbose");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateValue_ShouldAcceptValidAllowedValue()
    {
        var validator = new DefaultConfigurationValidator(_schema);

        var result = validator.ValidateValue("core.logging.level", "debug");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateValue_ShouldRejectNonStringValue_ForStringTypedKey()
    {
        var validator = new DefaultConfigurationValidator(_schema);

        var result = validator.ValidateValue("core.logging.level", 123);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateKey_ShouldRejectUnknownDefaultsKey_WhenForceIsFalse()
    {
        var validator = new DefaultConfigurationValidator(_schema);

        var result = validator.ValidateKey("defaults.unknown", force: false);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateValue_ShouldRejectNonBoolValue_ForAgentEnabled()
    {
        var validator = new DefaultConfigurationValidator(_schema);

        var result = validator.ValidateValue("agent.enabled", "notabool");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void GetAllKeys_ShouldDefaultAgentEnabledToFalse()
    {
        var key = _schema.GetAllKeys()
            .Single(k => string.Equals(k.Key, "agent.enabled", StringComparison.OrdinalIgnoreCase));

        // Must stay in sync with AgentOptions.Enabled = false (PP opt-in default).
        Assert.Equal("false", key.DefaultValue);
    }

    [Fact]
    public void ValidateValue_ShouldRejectNonBoolValue_ForCompletionEnabled()
    {
        var validator = new DefaultConfigurationValidator(_schema);

        var result = validator.ValidateValue("completion.enabled", "notabool");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void GetAllKeys_ShouldDefaultCompletionEnabledToTrue()
    {
        var key = _schema.GetAllKeys()
            .Single(k => string.Equals(k.Key, "completion.enabled", StringComparison.OrdinalIgnoreCase));

        // Must stay in sync with CompletionOptions.Enabled = true (opt-out default).
        Assert.Equal("true", key.DefaultValue);
    }

    [Fact]
    public void ValidateValue_ShouldRejectNonBoolValue_ForAgentInteractiveVerbose()
    {
        var validator = new DefaultConfigurationValidator(_schema);

        var result = validator.ValidateValue("agent.interactive.verbose", "notabool");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void GetAllKeys_ShouldDefaultAgentInteractiveVerboseToFalse()
    {
        var key = _schema.GetAllKeys()
            .Single(k => string.Equals(k.Key, "agent.interactive.verbose", StringComparison.OrdinalIgnoreCase));

        // Must stay in sync with InteractiveOptions.Verbose = false (clean-by-default).
        Assert.Equal("false", key.DefaultValue);
    }
}
