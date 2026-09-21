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
    public void Defaults_KeepMasterOffAndAutomaticStyleButDisableAutomaticTrigger()
    {
        var options = new AgentOptions();

        Assert.False(options.Enabled);
        Assert.False(options.AutoRecovery.Enabled);
        Assert.Equal("oneshot", options.SessionStyle);
        Assert.Equal(10, options.FailureHistory.MaxEntries);

        var schema = new ConfigurationSchema();
        Assert.Equal("false", schema.GetKeyMetadata("agent.autoRecovery.enabled")!.DefaultValue);
        var history = schema.GetKeyMetadata("agent.failureHistory.maxEntries")!;
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

        Assert.Equal(valid, validator.ValidateValue("agent.failureHistory.maxEntries", value).IsValid);
    }

    [Theory]
    [InlineData("oneshot", false)]
    [InlineData("interactive", false)]
    [InlineData("interactive", true)]
    public void ManualStyle_IsOneShotWithoutChangingConfiguredOptions(string configuredStyle, bool automaticEnabled)
    {
        var options = new AgentOptions
        {
            Enabled = true,
            SessionStyle = configuredStyle,
            AutoRecovery = new() { Enabled = automaticEnabled },
            Provider = "github",
            Builtin = new() { ModelId = "test-deployment" },
        };

        var actual = RecoverySessionSettings.Resolve(options, RecoveryTrigger.Manual);

        Assert.Equal("oneshot", actual.SessionStyle);
        Assert.False(actual.IsInteractive);
        Assert.Equal(RecoveryTrigger.Manual, actual.Trigger);
        Assert.Equal(configuredStyle, options.SessionStyle);
        Assert.Equal(automaticEnabled, options.AutoRecovery.Enabled);
        Assert.Equal("github", options.Provider);
        Assert.Equal("test-deployment", options.Builtin.ModelId);
    }

    [Theory]
    [InlineData("oneshot", false)]
    [InlineData("interactive", true)]
    [InlineData("INTERACTIVE", true)]
    public void AutomaticStyle_UsesConfiguredStyle(string style, bool interactive)
    {
        var actual = RecoverySessionSettings.Resolve(new() { SessionStyle = style }, RecoveryTrigger.Automatic);

        Assert.Equal(interactive, actual.IsInteractive);
        Assert.Equal(style.ToLowerInvariant(), actual.SessionStyle);
    }

    [Fact]
    public void AutomaticStyle_RejectsInvalidConfiguration()
    {
        Assert.Throws<ArgumentException>(() =>
            RecoverySessionSettings.Resolve(new() { SessionStyle = "manual" }, RecoveryTrigger.Automatic));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecoverySessionSettings.Resolve(new(), (RecoveryTrigger)99));
    }

    [Fact]
    public void Request_RoundTripsCompleteDiagnosticInput()
    {
        var args = new[] { "vm", "create", "--name", "name with spaces", "--password", "test-only-secret", "", "\u4e2d\u6587" };
        var error = "test-only-secret\r\n" + new string('x', 70_000) + "\n\"quoted\"";
        var record = new FailureRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Arguments = args,
            FailedCommand = "vm create --name \"name with spaces\"",
            ErrorText = error,
            ExitCode = 2,
            ErrorDetails = JsonSerializer.SerializeToElement(new { detail = error }),
        };

        var request = new RecoveryRequest { Failure = record };
        var actual = JsonSerializer.Deserialize<RecoveryRequest>(JsonSerializer.Serialize(request))!;

        Assert.Equal(RecoveryTrigger.Manual, actual.Trigger);
        Assert.Equal(record.Id, actual.Failure.Id);
        Assert.Equal(record.OccurredAtUtc, actual.Failure.OccurredAtUtc);
        Assert.Equal(args, actual.Failure.Arguments);
        Assert.Equal(error, actual.Failure.ErrorText);
        Assert.Equal(error, actual.Failure.ErrorDetails!.Value.GetProperty("detail").GetString());
    }

    [Fact]
    public void CompletionIndex_DoesNotContainDiagnosticInput()
    {
        var entry = new FailureHistoryEntry(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(entry));

        Assert.Equal(new[] { "Id", "OccurredAtUtc" }, json.RootElement.EnumerateObject().Select(property => property.Name));
    }
}
