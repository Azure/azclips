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
            "agent.interactive.verbose", "agent.provider", "agent.reasoningEffort", "agent.recovery.historySize", "agent.recovery.style", "agent.recovery.trigger", "agent.trace.enabled", "agent.trace.path",
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
    public void GetAllKeys_ShouldNotExposeBuiltinProviderKeys()
    {
        var keys = _schema.GetAllKeys();

        Assert.DoesNotContain(keys, key => key.Key.StartsWith("agent.builtin.", StringComparison.OrdinalIgnoreCase));
        Assert.False(_schema.IsKnownKey("agent.builtin.modelId"));
        Assert.False(_schema.IsKnownKey("agent.builtin.apiVersion"));
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

    [Theory]
    [InlineData("core.logging.level", "verbose", "debug, info, warn, error")]
    [InlineData("core.output.color", "adsf", "auto, always, never")]
    [InlineData("clients.cli.output.format", "xml", "json, jsonc, table, tsv, yaml, yamlc")]
    public void ValidateValue_ShouldRejectInvalidAllowedValue_WithAllowedValues(string key, string value, string allowedValues)
    {
        var validator = new DefaultConfigurationValidator(_schema);

        var result = validator.ValidateValue(key, value);

        Assert.False(result.IsValid);
        Assert.Equal($"Value '{value}' is not allowed for '{key}'. Allowed values: {allowedValues}.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("core.logging.level", "debug")]
    [InlineData("core.logging.level", "DEBUG")]
    [InlineData("core.output.color", "Always")]
    [InlineData("core.output.color", null)]
    [InlineData("defaults.group", "arbitrary-group-name")]
    [InlineData("made.up.key", "arbitrary-value")]
    public void ValidateValue_ShouldAcceptValidValue(string key, string? value)
    {
        var validator = new DefaultConfigurationValidator(_schema);

        var result = validator.ValidateValue(key, value);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateValue_ShouldRejectNonStringValue_ForStringTypedKey()
    {
        var validator = new DefaultConfigurationValidator(_schema);

        var result = validator.ValidateValue("core.logging.level", 123);

        Assert.False(result.IsValid);
        Assert.Equal("Value for 'core.logging.level' must be of type 'String'.", result.ErrorMessage);
    }

    [Fact]
    public void ValidateKey_ShouldRejectUnknownDefaultsKey_WhenForceIsFalse()
    {
        var validator = new DefaultConfigurationValidator(_schema);

        var result = validator.ValidateKey("defaults.unknown", force: false);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateValue_ShouldRejectUnknownValue_ForRecoveryTrigger()
    {
        var validator = new DefaultConfigurationValidator(_schema);

        var result = validator.ValidateValue("agent.recovery.trigger", "sometimes");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void GetAllKeys_ShouldDefaultRecoveryTriggerToOff()
    {
        var key = _schema.GetAllKeys()
            .Single(k => string.Equals(k.Key, "agent.recovery.trigger", StringComparison.OrdinalIgnoreCase));

        // Must stay in sync with RecoveryOptions.Trigger = "off" (PP opt-in default).
        Assert.Equal("off", key.DefaultValue);
    }

    [Fact]
    public void GetKeyMetadata_ShouldDescribeAgentReasoningEffort()
    {
        var key = _schema.GetKeyMetadata("agent.reasoningEffort");

        Assert.NotNull(key);
        Assert.Equal(typeof(string), key.ValueType);
        Assert.Equal("medium", key.DefaultValue);
        Assert.True(key.IsRequired);
        Assert.Equal(new AgentOptions().ReasoningEffort, key.DefaultValue);
        Assert.Equal(new[] { "low", "medium", "high" }, key.AllowedValues);
    }

    [Fact]
    public void ValidateValue_ShouldPreserveNullForOptionalKeys()
    {
        var validator = new DefaultConfigurationValidator(_schema);

        Assert.True(validator.ValidateValue("defaults.group", null).IsValid);
        Assert.True(validator.ValidateValue("experimental.optional", null).IsValid);
        Assert.False(validator.ValidateValue("agent.reasoningEffort", null).IsValid);
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
