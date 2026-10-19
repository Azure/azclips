// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Configuration.Implementation;
using Core.Configuration.Schema;

namespace Cli.Tests.Recovery;

public class RecoveryContractTests
{
    [Fact]
    public void Defaults_AreManualOneShotAndTen()
    {
        var options = new AgentOptions();
        Assert.True(options.Recovery.IsEnabled);
        Assert.False(options.Recovery.IsAutomatic);
        Assert.False(options.Recovery.IsInteractive);
        Assert.Equal("manual", options.Recovery.Trigger);
        Assert.Equal("oneshot", options.Recovery.Style);
        Assert.Equal(10, options.Recovery.HistorySize);
        var schema = new ConfigurationSchema();
        Assert.Equal("manual", schema.GetKeyMetadata("agent.recovery")!.DefaultValue);
        var history = schema.GetKeyMetadata("agent.recovery.historySize")!;
        Assert.Equal("10", history.DefaultValue);
        Assert.Equal(1, history.Minimum);
        Assert.Equal(typeof(int), history.ValueType);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData("not-a-number", false)]
    [InlineData("2147483648", false)]
    public void HistoryLimit_RequiresPositiveInteger(object value, bool valid)
    {
        var validator = new DefaultConfigurationValidator(new ConfigurationSchema());
        Assert.Equal(valid, validator.ValidateValue("agent.recovery.historySize", value).IsValid);
    }

    [Theory]
    [InlineData("manual", "oneshot")]
    [InlineData("manual", "interactive")]
    [InlineData("auto", "oneshot")]
    [InlineData("auto", "interactive")]
    [InlineData("off", "oneshot")]
    [InlineData("off", "interactive")]
    public void TriggerAndStyle_AreOrthogonal(string trigger, string style)
    {
        var options = new AgentOptions
        {
            Recovery = new() { Trigger = trigger, Style = style },
            Provider = "github",
            Builtin = new() { ModelId = "test-deployment" },
        };
        var manual = RecoverySessionSettings.Resolve(options, RecoveryTrigger.Manual);
        var automatic = RecoverySessionSettings.Resolve(options, RecoveryTrigger.Automatic);
        Assert.Equal(style, manual.SessionStyle);
        Assert.Equal(style, automatic.SessionStyle);
        Assert.Equal(trigger != "off", options.Recovery.IsEnabled);
        Assert.Equal(trigger == "auto", options.Recovery.IsAutomatic);
        Assert.Equal(style == "interactive", options.Recovery.IsInteractive);
        Assert.Equal("github", options.Provider);
        Assert.Equal("test-deployment", options.Builtin.ModelId);
    }

    [Fact]
    public void StyleOverride_DoesNotMutatePersistentOptions()
    {
        var options = new AgentOptions();
        var actual = RecoverySessionSettings.Resolve(options, RecoveryTrigger.Manual, "INTERACTIVE");
        Assert.True(actual.IsInteractive);
        Assert.Equal("oneshot", options.Recovery.Style);
        Assert.Throws<ArgumentException>(() => RecoverySessionSettings.Resolve(options, RecoveryTrigger.Manual, "manual"));
    }

    [Theory]
    [InlineData("wrong", "oneshot", 10)]
    [InlineData("manual", "wrong", 10)]
    [InlineData("manual", "oneshot", 0)]
    public void InvalidOptions_AreNotSilentlyConvertedToADifferentPolicy(string trigger, string style, int size)
    {
        var options = new RecoveryOptions { Trigger = trigger, Style = style, HistorySize = size };
        Assert.ThrowsAny<ArgumentException>(() => options.Validate());
        Assert.ThrowsAny<ArgumentException>(() => options.IsEnabled);
    }

    [Fact]
    public void Request_RoundTripsCompleteDiagnosticInput()
    {
        var error = "test-only-secret\r\n" + new string('x', 70_000) + "\n\"quoted\"";
        var request = new RecoveryRequest
        {
            FailedCommand = "vm create --name \"name with spaces\"",
            ErrorText = error,
            FailureId = "48213-2026-0923-1004-0000",
        };
        var actual = JsonSerializer.Deserialize<RecoveryRequest>(JsonSerializer.Serialize(request))!;
        Assert.Equal(RecoveryTrigger.Manual, actual.Trigger);
        Assert.Equal(request.FailureId, actual.FailureId);
        Assert.Equal(request.FailedCommand, actual.FailedCommand);
        Assert.Equal(error, actual.ErrorText);
    }

    [Fact]
    public void CompletionIndex_ContainsOnlyIdTimestampAndEncryptionFlag()
    {
        var entry = new FailureHistoryEntry("48213-2026-0923-1004-0000", DateTimeOffset.UtcNow);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(entry));
        Assert.Equal(new[] { "Id", "OccurredAtUtc", "Unencrypted" }, json.RootElement.EnumerateObject().Select(property => property.Name));
    }
}
