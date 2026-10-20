// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Tests.Common.Utilities;

namespace Core.Telemetry.Tests;

public class RecoveryTelemetryTests
{
    [Theory]
    [InlineData("builtin", "custom-deployment", "custom-deployment", "configuration")]
    [InlineData("BUILTIN", "custom-deployment", "custom-deployment", "configuration")]
    [InlineData("builtin", "", null, "unavailable")]
    [InlineData("builtin", "   ", null, "unavailable")]
    [InlineData("github", "gpt-5.4", null, "unavailable")]
    public void RecordConfiguration_UsesOnlyEffectiveBuiltinModel(
        string provider, string configuredModel, string? expectedModel, string expectedSource)
    {
        using var activity = new Activity("recovery").Start();
        var options = new AgentOptions
        {
            Provider = provider,
            Builtin = new BuiltinProviderOptions { ModelId = configuredModel },
            Recovery = new() { Style = "interactive" },
        };

        RecoveryTelemetry.RecordConfiguration(activity, options);

        Assert.Equal(expectedModel, activity.GetTagItem(TelemetryTags.AgentModel));
        Assert.Equal(expectedSource, activity.GetTagItem(TelemetryTags.AgentModelSource));
        Assert.Equal(provider, activity.GetTagItem(TelemetryTags.RecoveryProvider));
        Assert.Equal("interactive", activity.GetTagItem(TelemetryTags.RecoverySessionStyle));
    }

    [Theory]
    [InlineData("interactive", "oneshot", "oneshot")]
    [InlineData("oneshot", "interactive", "interactive")]
    [InlineData("interactive", null, "interactive")]
    public void RecordConfiguration_RecordsTheEffectiveStyleWhenOverridden(
        string configured, string? effective, string expected)
    {
        using var activity = new Activity("Agent.Create").Start();
        RecoveryTelemetry.RecordConfiguration(activity, new AgentOptions { Recovery = new RecoveryOptions { Style = configured } }, effective);
        Assert.Equal(expected, activity.GetTagItem(TelemetryTags.RecoverySessionStyle));
    }

    [Fact]
    public void EveryModelSource_HasADistinctWireForm()
    {
        var sources = Enum.GetValues<AgentModelSource>();

        var wireForms = sources.Select(source => source.Wire()).ToArray();

        var duplicates = wireForms.GroupBy(wire => wire, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        Assert.True(duplicates.Length == 0,
            "agent.modelSource is a closed set, so every member must map to its own wire form. " +
            $"Shared by more than one member: {string.Join(", ", duplicates)}.");
        Assert.All(wireForms, wire => Assert.True(
            wire.Length > 0 && wire == wire.ToLowerInvariant(),
            $"Wire forms are lowercase and non-empty; got '{wire}'."));

        // telemetry.md claims a new source cannot reach the exporter without being documented.
        // Distinctness alone does not make that true -- adding a member with its own wire form
        // would have shipped undocumented and left this test green. Pinning the set is what
        // turns the doc's claim into something enforced rather than asserted.
        Assert.Equal(
            new[] { "unavailable", "configuration", "sdk", "usage" }.OrderBy(w => w, StringComparer.Ordinal),
            wireForms.OrderBy(w => w, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RecordModel_WithNullOrWhitespaceModel_ReportsUnavailable(string? model)
    {
        using var activity = new Activity("recovery").Start();

        RecoveryTelemetry.RecordModel(activity, model, AgentModelSource.Sdk);

        var recorded = activity.GetTagItem(TelemetryTags.AgentModel);
        Assert.True(recorded is null,
            $"A blank model name is not a model; it must not be exported. Recorded '{recorded}'.");
        var source = activity.GetTagItem(TelemetryTags.AgentModelSource);
        Assert.True(Equals(source, AgentModelSource.Unavailable.Wire()),
            $"A blank model must report modelSource=unavailable, not the claimed source. Reported '{source}'.");
    }

    public static TheoryData<string, bool> DirectModelValues => new()
    {
        { "model with spaces", false },
        { "gpt-5\n", false },
        { "gpt\0name", false },
        { "\u6a21\u578b", false },
        { new string('a', 129), false },
        { new string('a', 128), true },
        { "deployment.v2:model-1_0", true },
    };

    [Theory]
    [MemberData(nameof(DirectModelValues))]
    public void RecordModel_EnforcesExportBoundsAtTheWriter(string model, bool exportable)
    {
        using var activity = new Activity("recovery").Start();
        RecoveryTelemetry.RecordModel(activity, "previous-valid-model", AgentModelSource.Sdk);

        RecoveryTelemetry.RecordModel(activity, model, AgentModelSource.Usage);

        Assert.Equal(exportable ? model : null, activity.GetTagItem(TelemetryTags.AgentModel));
        Assert.Equal((exportable ? AgentModelSource.Usage : AgentModelSource.Unavailable).Wire(),
            activity.GetTagItem(TelemetryTags.AgentModelSource));
    }

    [Fact]
    public void RecordConfiguration_NoOps_WhenActivityIsMissing()
    {
        var options = new AgentOptions();

        RecoveryTelemetry.RecordConfiguration(null, options);

        Assert.Equal("gpt-5.4-mini", RecoveryTelemetry.GetConfiguredModel(options));
    }

    [Fact]
    public void ConfiguredModel_SurvivesFileExportAndReplay_WhenStartupEndsWithoutSend()
    {
        var directory = Path.Combine(Path.GetTempPath(), "azclips-model-export-" + Guid.NewGuid().ToString("N"));
        var sourceName = "model-export-" + Guid.NewGuid().ToString("N");
        try
        {
            using var source = new ActivitySource(sourceName);
            using (var provider = Sdk.CreateTracerProviderBuilder()
                       .AddSource(sourceName)
                       .SetSampler(new AlwaysOnSampler())
                       .AddProcessor(new SimpleActivityExportProcessor(new FileExporter(directory)))
                       .Build())
            {
                var options = new AgentOptions { Builtin = new() { ModelId = "configured-before-startup" } };
                using var root = source.StartActivity("CLI.Activity.AiRecovery", ActivityKind.Server);
                RecoveryTelemetry.RecordConfiguration(root, options);
                Assert.Equal("configured-before-startup", root?.GetTagItem(TelemetryTags.AgentModel));
                using var startup = source.StartActivity("Agent.Create");
                RecoveryTelemetry.RecordConfiguration(startup, options);
                startup?.SetStatus(ActivityStatusCode.Error);
            }

            var entries = Directory.GetFiles(directory, TraceFileStore.PendingFilePattern)
                .SelectMany(path => TraceFileFormat.Parse(File.ReadAllText(path))).ToArray();
            Assert.Equal(2, entries.Length);
            Assert.DoesNotContain(entries, entry => entry.OperationName == "Agent.Send");

            using var replaySource = new ActivitySource("model-replay-" + Guid.NewGuid().ToString("N"));
            var replayed = new List<Activity>();
            using var listener = new ActivityListener
            {
                ShouldListenTo = candidate => candidate == replaySource,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStopped = replayed.Add,
            };
            ActivitySource.AddActivityListener(listener);

            TraceReplayer.ReplayEntries(replaySource, entries);

            Assert.Equal(2, replayed.Count);
            Assert.All(replayed, span =>
            {
                Assert.Equal("configured-before-startup", span.GetTagItem(TelemetryTags.AgentModel));
                Assert.Equal("configuration", span.GetTagItem(TelemetryTags.AgentModelSource));
            });
        }
        finally
        {
            DirectoryUtils.TryDeleteDirectory(directory);
        }
    }
}
