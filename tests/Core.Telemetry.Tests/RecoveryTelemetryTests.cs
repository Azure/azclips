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
            SessionStyle = "interactive",
        };

        RecoveryTelemetry.RecordConfiguration(activity, options);

        Assert.Equal(expectedModel, activity.GetTagItem(TelemetryTags.AgentModel));
        Assert.Equal(expectedSource, activity.GetTagItem(TelemetryTags.AgentModelSource));
        Assert.Equal(provider, activity.GetTagItem(TelemetryTags.RecoveryProvider));
        Assert.Equal("interactive", activity.GetTagItem(TelemetryTags.RecoverySessionStyle));
    }

    [Fact]
    public void RecordConfiguration_RespectsMissingActivity_WhenTelemetryIsDisabled()
    {
        var options = new AgentOptions();

        RecoveryTelemetry.RecordConfiguration(null, options);

        Assert.Equal("gpt-5.4", RecoveryTelemetry.GetConfiguredModel(options));
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
