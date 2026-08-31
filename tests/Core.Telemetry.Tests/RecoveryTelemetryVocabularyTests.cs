// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry.Tests;

/// <summary>
/// The AI error-recovery spans (Azure/CLIPS#602) are dropped without a trace if their
/// names are missing from the activity-name allowlist, and their tags are stripped if
/// they fall outside the property allowlist. Both failures are silent, so they are
/// pinned here rather than discovered in the portal.
/// </summary>
public class RecoveryTelemetryVocabularyTests
{
    private readonly DefaultTelemetryActivityNameFilter _names = new();
    private readonly DefaultTelemetryPropertyFilter _properties = new();

    [Theory]
    [InlineData("CLI.Activity.AiRecovery")]
    [InlineData("Agent.Session")]
    [InlineData("Agent.Create")]
    [InlineData("Agent.Turn")]
    [InlineData("Agent.Send")]
    [InlineData("tool.get_command")]
    [InlineData("tool.bash")]
    public void IsAllowed_AdmitsRecoverySpanNames(string name) =>
        Assert.True(_names.IsAllowed(name));

    [Theory]
    [InlineData("Agent.NotARealSpan")]
    [InlineData("toolbox.something")]
    public void IsAllowed_StillRejectsUnknownNames(string name) =>
        Assert.False(_names.IsAllowed(name));

    [Theory]
    [InlineData(nameof(TelemetryTags.CommandTraceId))]
    [InlineData(nameof(TelemetryTags.CommandSpanId))]
    [InlineData(nameof(TelemetryTags.RecoverySessionStyle))]
    [InlineData(nameof(TelemetryTags.RecoveryProvider))]
    [InlineData(nameof(TelemetryTags.RecoveryExitCode))]
    [InlineData(nameof(TelemetryTags.RecoveryTurns))]
    [InlineData(nameof(TelemetryTags.AgentModel))]
    [InlineData(nameof(TelemetryTags.ProcessName))]
    public void RecoveryTags_AreNamedForTheProductPropertyChannel(string constantName)
    {
        var value = (string)typeof(TelemetryTags).GetField(constantName)!.GetValue(null)!;

        Assert.StartsWith(TelemetryTags.PropertyTagPrefix, value, StringComparison.Ordinal);
        Assert.True(_properties.IsAllowed(value));
    }
}
