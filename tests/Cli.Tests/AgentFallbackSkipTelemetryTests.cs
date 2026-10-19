// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Cli;
using Core.Telemetry;
using Moq;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Cli.Tests;

[Collection("Azclips Service Provider Tests")]
public class AgentFallbackSkipTelemetryTests
{
    private const string SensitiveValue = "test-only-private-argument";
    private const string ErrorText = "test-only private error detail";

    [Fact]
    public async Task DisabledFallback_EmitsSeparateRoot_WithInstallationIdAndCommandCorrelation()
    {
        using var harness = new Harness();
        var command = harness.Telemetry.StartRootActivity();
        Assert.NotNull(command);
        command.SetTag(TelemetryTags.Command, "profile show");

        var exitCode = await Program.StopRootThenFallbackAsync(
            command,
            (traceId, spanId, commandName) => Program.TryAgentFallbackAsync(
                () => harness.Options, harness.Telemetry,
                ["profile", "show", SensitiveValue, "--token", SensitiveValue],
                ErrorText, traceId, spanId, commandName));

        Assert.Null(exitCode);
        Assert.Equal(new[] { "CLI.Activity.Root", "CLI.Activity.AiRecoverySkipped" },
            harness.Exported.Select(activity => activity.Name));
        Assert.DoesNotContain(harness.Exported, activity => activity.Name == "CLI.Activity.AiRecovery");
        var skipped = harness.Exported[1];
        Assert.NotEqual(command.TraceId, skipped.TraceId);
        Assert.Equal(default, skipped.ParentSpanId);
        Assert.Equal("disabled", skipped.Tags.GetValueOrDefault(TelemetryTags.RecoverySkipReason));
        Assert.Equal("profile show", skipped.Tags.GetValueOrDefault(TelemetryTags.Command));
        Assert.Equal(command.TraceId.ToString(), skipped.Tags.GetValueOrDefault(TelemetryTags.CommandTraceId));
        Assert.Equal(command.SpanId.ToString(), skipped.Tags.GetValueOrDefault(TelemetryTags.CommandSpanId));
        Assert.Equal(TelemetryPropertyResolver.InstallationId,
            skipped.Tags.GetValueOrDefault(TelemetryTags.InstallationId));
        Assert.False(string.IsNullOrEmpty(skipped.Tags.GetValueOrDefault(TelemetryTags.InstallationId) as string));
        Assert.Equal("CLI", skipped.Tags.GetValueOrDefault(TelemetryTags.ClientType));
        Assert.Equal("CLI.Activity.AiRecoverySkipped", skipped.Tags.GetValueOrDefault(TelemetryTags.EventName));
        Assert.Equal(ActivityStatusCode.Unset, skipped.Status);
        Assert.Null(skipped.StatusDescription);
        Assert.Empty(skipped.Events);
        Assert.False(skipped.Tags.ContainsKey(TelemetryTags.Parameter));
        Assert.False(skipped.Tags.ContainsKey(TelemetryTags.RecoveryExitCode));
        Assert.DoesNotContain(skipped.Tags.Values, value => value is string text &&
            (text.Contains(SensitiveValue, StringComparison.Ordinal) ||
             text.Contains(ErrorText, StringComparison.Ordinal)));
    }

    public static TheoryData<string[], string?> UnresolvedCommands => new()
    {
        { ["vm", "lst", "--name", SensitiveValue], "vm" },
        { ["profile", "show", SensitiveValue], "profile" },
        { ["--output", SensitiveValue], null },
        { [], null },
    };

    [Theory]
    [MemberData(nameof(UnresolvedCommands))]
    public async Task DisabledFallback_OnlyRecordsCommandGroup_WhenParsingDidNotResolveCommand(
        string[] args, string? expectedCommand)
    {
        using var harness = new Harness();

        var exitCode = await Program.TryAgentFallbackAsync(
            () => harness.Options, harness.Telemetry, args, ErrorText, null, null, null);

        Assert.Null(exitCode);
        var skipped = Assert.Single(harness.Exported);
        Assert.Equal("CLI.Activity.AiRecoverySkipped", skipped.Name);
        Assert.Equal(expectedCommand, skipped.Tags.GetValueOrDefault(TelemetryTags.Command));
        Assert.False(skipped.Tags.ContainsKey(TelemetryTags.CommandTraceId));
        Assert.False(skipped.Tags.ContainsKey(TelemetryTags.CommandSpanId));
        Assert.DoesNotContain(skipped.Tags.Values, value => value is string text &&
            text.Contains(SensitiveValue, StringComparison.Ordinal));
    }

    [Fact]
    public async Task DisabledFallback_DoesNotEmitActivities_WhenTelemetryIsDisabled()
    {
        using var harness = new Harness(telemetryEnabled: false);

        var exitCode = await Program.TryAgentFallbackAsync(
            () => harness.Options, harness.Telemetry, ["vm", "lst"], ErrorText, null, null, null);

        Assert.Null(exitCode);
        Assert.Empty(harness.Exported);
    }

    [Fact]
    public async Task DisabledFallback_DoesNotEmitActivities_WhenTelemetryIsUnavailable()
    {
        using var harness = new Harness();

        var exitCode = await Program.TryAgentFallbackAsync(
            () => harness.Options, null, ["vm", "lst"], ErrorText, null, null, null);

        Assert.Null(exitCode);
        Assert.Empty(harness.Exported);
    }

    [Fact]
    public async Task UnreadableConfiguration_DoesNotEmitDisabledSkip()
    {
        using var harness = new Harness();

        var exitCode = await Program.TryAgentFallbackAsync(
            () => throw new InvalidOperationException("test-only configuration failure"),
            harness.Telemetry, ["vm", "lst"], ErrorText, null, null, null);

        Assert.Null(exitCode);
        Assert.Empty(harness.Exported);
    }

    [Fact]
    public async Task EnabledRecursionGuard_DoesNotEmitDisabledSkip()
    {
        using var harness = new Harness();
        harness.Options.Enabled = true;
        var previous = Environment.GetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE");
        Environment.SetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE", "1");
        try
        {
            var exitCode = await Program.TryAgentFallbackAsync(
                () => harness.Options, harness.Telemetry, ["vm", "lst"], ErrorText, null, null, null);

            Assert.Null(exitCode);
            Assert.Empty(harness.Exported);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE", previous);
        }
    }

    [Fact]
    public async Task ExcludedCommand_SkipsRecovery_AndEmitsExcludedSkipWithoutListEntries()
    {
        using var harness = new Harness();
        harness.Options.Enabled = true;
        var command = harness.Telemetry.StartRootActivity();
        Assert.NotNull(command);
        command.SetTag(TelemetryTags.Command, "vm list");

        var exitCode = await Program.StopRootThenFallbackAsync(
            command,
            (traceId, spanId, commandName) => Program.TryAgentFallbackAsync(
                () => harness.Options, harness.Telemetry,
                ["vm", "list", SensitiveValue], ErrorText, traceId, spanId, commandName,
                getExcludedCommands: () => "vm,test-only-entry",
                commandTokens: ["vm", "list"], commandResolved: true));

        Assert.Null(exitCode);
        Assert.Equal(new[] { "CLI.Activity.Root", "CLI.Activity.AiRecoverySkipped" },
            harness.Exported.Select(activity => activity.Name));
        var skipped = harness.Exported[1];
        Assert.Equal("excluded", skipped.Tags.GetValueOrDefault(TelemetryTags.RecoverySkipReason));
        Assert.Equal("vm list", skipped.Tags.GetValueOrDefault(TelemetryTags.Command));
        Assert.Equal(command.TraceId.ToString(), skipped.Tags.GetValueOrDefault(TelemetryTags.CommandTraceId));
        Assert.Equal(command.SpanId.ToString(), skipped.Tags.GetValueOrDefault(TelemetryTags.CommandSpanId));
        Assert.Equal(default, skipped.ParentSpanId);
        Assert.DoesNotContain(skipped.Tags.Values, value => value is string text &&
            (text.Contains("test-only-entry", StringComparison.Ordinal) ||
             text.Contains("vm,", StringComparison.Ordinal) ||
             text.Contains(SensitiveValue, StringComparison.Ordinal) ||
             text.Contains(ErrorText, StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("vm", new[] { "vm", "create" }, true, true)]
    [InlineData("az VM  Create", new[] { "vm", "create" }, true, true)]
    [InlineData("config", new[] { "configuration", "show" }, true, false)]
    [InlineData("vm create", new[] { "vm" }, true, false)]
    [InlineData("vm list", new[] { "vm", "list" }, false, false)]
    [InlineData("vm", new[] { "vm", "lst" }, false, true)]
    [InlineData("config nonexistent", new[] { "config", "nonexistent" }, false, false)]
    [InlineData("config", new[] { "config", "nonexistent" }, false, true)]
    [InlineData("config set", new[] { "config", "set" }, true, true)]
    public async Task ExclusionMatching_FollowsTokenPrefixRules(
        string list, string[] tokens, bool resolved, bool expectExcluded)
    {
        using var harness = new Harness();
        harness.Options.Enabled = true;
        var previous = Environment.GetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE");
        Environment.SetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE", "1");
        try
        {
            var exitCode = await Program.TryAgentFallbackAsync(
                () => harness.Options, harness.Telemetry, tokens, ErrorText, null, null, null,
                getExcludedCommands: () => list, commandTokens: tokens, commandResolved: resolved);

            Assert.Null(exitCode);
            if (expectExcluded)
            {
                var skipped = Assert.Single(harness.Exported);
                Assert.Equal("excluded", skipped.Tags.GetValueOrDefault(TelemetryTags.RecoverySkipReason));
            }
            else
            {
                Assert.Empty(harness.Exported);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE", previous);
        }
    }

    [Fact]
    public async Task ExclusionListReadFailure_TreatedAsEmpty_RecoveryProceeds()
    {
        using var harness = new Harness();
        harness.Options.Enabled = true;
        var previous = Environment.GetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE");
        Environment.SetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE", "1");
        try
        {
            var exitCode = await Program.TryAgentFallbackAsync(
                () => harness.Options, harness.Telemetry, ["vm", "list"], ErrorText, null, null, null,
                getExcludedCommands: () => throw new InvalidOperationException("test-only read failure"),
                commandTokens: ["vm", "list"], commandResolved: true);

            Assert.Null(exitCode);
            Assert.Empty(harness.Exported);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE", previous);
        }
    }

    [Fact]
    public async Task ExcludedCommand_DoesNotEmitActivities_WhenTelemetryIsDisabled()
    {
        using var harness = new Harness(telemetryEnabled: false);
        harness.Options.Enabled = true;

        var exitCode = await Program.TryAgentFallbackAsync(
            () => harness.Options, harness.Telemetry, ["vm", "list"], ErrorText, null, null, null,
            getExcludedCommands: () => "vm", commandTokens: ["vm", "list"], commandResolved: true);

        Assert.Null(exitCode);
        Assert.Empty(harness.Exported);
    }

    [Fact]
    public async Task DisabledAgent_DoesNotReadExclusionList()
    {
        using var harness = new Harness();
        var read = false;

        var exitCode = await Program.TryAgentFallbackAsync(
            () => harness.Options, harness.Telemetry, ["vm", "list"], ErrorText, null, null, null,
            getExcludedCommands: () => { read = true; return "vm"; },
            commandTokens: ["vm", "list"], commandResolved: true);

        Assert.Null(exitCode);
        Assert.False(read);
        var skipped = Assert.Single(harness.Exported);
        Assert.Equal("disabled", skipped.Tags.GetValueOrDefault(TelemetryTags.RecoverySkipReason));
    }

    private sealed record ExportedActivity(
        string Name, ActivityTraceId TraceId, ActivitySpanId ParentSpanId,
        ActivityStatusCode Status, string? StatusDescription,
        Dictionary<string, object?> Tags, ActivityEvent[] Events);

    private sealed class Harness : IDisposable
    {
        private readonly TestTelemetryProvider _provider;

        public AgentOptions Options { get; } = new() { Enabled = false, SessionStyle = "oneshot" };
        public List<ExportedActivity> Exported { get; } = [];
        public TelemetryService Telemetry { get; }

        public Harness(bool telemetryEnabled = true)
        {
            var configuration = new Mock<IConfigurationService>();
            configuration.Setup(config => config.GetConfiguration<TelemetryOptions>(
                    It.IsAny<string?>(), It.IsAny<TelemetryOptions?>()))
                .Returns(new TelemetryOptions { Enabled = telemetryEnabled });
            _provider = new TestTelemetryProvider(Exported);
            Telemetry = new TelemetryService(_provider, configuration.Object, new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));
        }

        public void Dispose()
        {
            Telemetry.Dispose();
            _provider.ActivitySource.Dispose();
        }
    }

    private sealed class TestTelemetryProvider : DefaultTelemetryProvider
    {
        public TestTelemetryProvider(List<ExportedActivity> exported)
        {
            ActivitySource = new ActivitySource("cli-skip-test-" + Guid.NewGuid().ToString("N"));
            TracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySource.Name)
                .AddProcessor(new DefaultActivityNameFilterProcessor(new DefaultTelemetryActivityNameFilter()))
                .AddProcessor(new DefaultTelemetryPropertyFilterProcessor(new DefaultTelemetryPropertyFilter()))
                .AddProcessor(new SimpleActivityExportProcessor(new SnapshotExporter(exported)))
                .Build()!;
        }
    }

    private sealed class SnapshotExporter(List<ExportedActivity> exported) : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
            {
                exported.Add(new ExportedActivity(
                    activity.OperationName, activity.TraceId, activity.ParentSpanId,
                    activity.Status, activity.StatusDescription,
                    activity.TagObjects.ToDictionary(tag => tag.Key, tag => tag.Value),
                    activity.Events.ToArray()));
            }
            return ExportResult.Success;
        }
    }
}
