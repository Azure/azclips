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
    public void Defaults_AreOffOneShotAndTen()
    {
        var options = new AgentOptions();
        Assert.False(options.IsRecoveryEnabled);
        Assert.False(options.IsRecoveryAutomatic);
        Assert.False(options.IsInteractive);
        Assert.Equal("off", options.Recovery);
        Assert.Equal("oneshot", options.SessionStyle);
        Assert.Equal(10, AgentOptions.RecoveryHistorySize);
        var schema = new ConfigurationSchema();
        Assert.Equal("off", schema.GetKeyMetadata("agent.recovery")!.DefaultValue);
        Assert.False(schema.IsKnownKey("agent.recovery.historySize"));
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
            Recovery = trigger,
            SessionStyle = style,
            Provider = "github",
            Builtin = new() { ModelId = "test-deployment" },
        };
        var manual = RecoverySessionSettings.Resolve(options, RecoveryTrigger.Manual);
        var automatic = RecoverySessionSettings.Resolve(options, RecoveryTrigger.Automatic);
        Assert.Equal(style, manual.SessionStyle);
        Assert.Equal(style, automatic.SessionStyle);
        Assert.Equal(trigger != "off", options.IsRecoveryEnabled);
        Assert.Equal(trigger == "auto", options.IsRecoveryAutomatic);
        Assert.Equal(style == "interactive", options.IsInteractive);
        Assert.Equal("github", options.Provider);
        Assert.Equal("test-deployment", options.Builtin.ModelId);
    }

    [Fact]
    public void StyleOverride_DoesNotMutatePersistentOptions()
    {
        var options = new AgentOptions();
        var actual = RecoverySessionSettings.Resolve(options, RecoveryTrigger.Manual, "INTERACTIVE");
        Assert.True(actual.IsInteractive);
        Assert.Equal("oneshot", options.SessionStyle);
        Assert.Throws<ArgumentException>(() => RecoverySessionSettings.Resolve(options, RecoveryTrigger.Manual, "manual"));
    }

    [Fact]
    public void InvalidRecovery_IsNotSilentlyConvertedToADifferentPolicy()
    {
        var options = new AgentOptions { Recovery = "wrong" };
        Assert.Throws<ArgumentException>(() => options.IsRecoveryEnabled);
        Assert.Throws<ArgumentException>(() => options.IsRecoveryAutomatic);
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
