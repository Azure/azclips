// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
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
        command.SetTag(TelemetryTags.DataBoundary, "EU");
        command.SetTag(TelemetryTags.TenantId, "tenant-eu");
        command.SetTag(TelemetryTags.SubscriptionId, "subscription-eu");

        var exitCode = await Program.StopRootThenFallbackAsync(
            command, true,
            context => harness.InvokeAsync(
                ["profile", "show", SensitiveValue, "--token", SensitiveValue],
                ErrorText, context));

        Assert.Equal(1, exitCode);
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
        Assert.Equal("Agent", skipped.Tags.GetValueOrDefault(TelemetryTags.ClientType));
        Assert.Equal("Agent", skipped.Tags.GetValueOrDefault(TelemetryLayerTags.ServiceName));
        Assert.Equal("agent-v1", skipped.Tags.GetValueOrDefault(TelemetryLayerTags.Schema));
        Assert.Equal("CLI.Activity.AiRecoverySkipped", skipped.Tags.GetValueOrDefault(TelemetryTags.EventName));
        Assert.Equal(ActivityStatusCode.Unset, skipped.Status);
        Assert.Null(skipped.StatusDescription);
        Assert.Empty(skipped.Events);
        Assert.False(skipped.Tags.ContainsKey(TelemetryTags.Parameter));
        Assert.False(skipped.Tags.ContainsKey(TelemetryTags.RecoveryExitCode));
        Assert.DoesNotContain(skipped.Tags.Values, value => value is string text &&
            (text.Contains(SensitiveValue, StringComparison.Ordinal) ||
             text.Contains(ErrorText, StringComparison.Ordinal)));
        var persisted = AssertPublishedAgentRecord(
            harness, "CLI.Activity.AiRecoverySkipped", "profile show", "EU");
        Assert.Equal(command.TraceId.ToString(), StringTag(persisted, TelemetryTags.CommandTraceId));
        Assert.Equal(command.SpanId.ToString(), StringTag(persisted, TelemetryTags.CommandSpanId));
        Assert.Equal("tenant-eu", StringTag(persisted, TelemetryTags.TenantId));
        Assert.Equal("subscription-eu", StringTag(persisted, TelemetryTags.SubscriptionId));
        Assert.Equal(skipped.Tags[TelemetryLayerTags.InvocationId],
            StringTag(persisted, TelemetryLayerTags.InvocationId));
    }

    public static TheoryData<string[], string?> UnresolvedCommands => new()
    {
        { ["__unknown_group__", "--name", SensitiveValue], "__unknown_group__" },
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

        var exitCode = await harness.InvokeAsync(args, ErrorText);

        Assert.Equal(1, exitCode);
        var skipped = Assert.Single(harness.Exported);
        Assert.Equal("CLI.Activity.AiRecoverySkipped", skipped.Name);
        Assert.Equal(expectedCommand, skipped.Tags.GetValueOrDefault(TelemetryTags.Command));
        Assert.False(skipped.Tags.ContainsKey(TelemetryTags.CommandTraceId));
        Assert.False(skipped.Tags.ContainsKey(TelemetryTags.CommandSpanId));
        Assert.DoesNotContain(skipped.Tags.Values, value => value is string text &&
            text.Contains(SensitiveValue, StringComparison.Ordinal));
        var persisted = AssertPublishedAgentRecord(
            harness, "CLI.Activity.AiRecoverySkipped", expectedCommand, "Global");
        Assert.False(persisted.ContainsKey(TelemetryTags.CommandTraceId));
        Assert.False(persisted.ContainsKey(TelemetryTags.CommandSpanId));
        Assert.Equal("disabled", StringTag(persisted, TelemetryTags.RecoverySkipReason));
    }

    [Theory]
    [MemberData(nameof(UnresolvedCommands))]
    public async Task StartedFallback_PersistsTheSameCommandGroupAsTheChildLaunch(
        string[] args, string? expectedCommand)
    {
        using var harness = new Harness();
        harness.Options.Enabled = true;
        var context = new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), true,
            "11111111111111111111111111111111", "2222222222222222", DataBoundary: "EU");
        TelemetryInvocationContext? childContext = null;

        var exitCode = await harness.InvokeAsync(args, ErrorText, context, runAgent: startInfo =>
        {
            Assert.True(TelemetryLayerEnvironment.TryReadAgentLaunch(
                key => startInfo.Environment.TryGetValue(key, out var value) ? value : null,
                out childContext, out _, out var diagnostic));
            Assert.Null(diagnostic);
            return Task.FromResult(1);
        });

        Assert.Equal(1, exitCode);
        Assert.NotNull(childContext);
        var persisted = AssertPublishedAgentRecord(
            harness, "CLI.Activity.AiRecovery", expectedCommand, "EU");
        Assert.Equal(expectedCommand, string.IsNullOrEmpty(childContext.Command) ? null : childContext.Command);
        Assert.Equal(context.InvocationId, childContext.InvocationId);
        Assert.Equal(context.InvocationId, StringTag(persisted, TelemetryLayerTags.InvocationId));
        Assert.Equal(context.CommandTraceId, StringTag(persisted, TelemetryTags.CommandTraceId));
        Assert.Equal(context.CommandSpanId, StringTag(persisted, TelemetryTags.CommandSpanId));
        Assert.Equal(1, persisted[TelemetryTags.RecoveryExitCode].GetInt32());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyResolvedCommand_IsFilledBeforeTheScopeSnapshot(bool enabled)
    {
        using var harness = new Harness();
        harness.Options.Enabled = enabled;
        var context = new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), true, Command: "");
        Func<ProcessStartInfo, Task<int>>? runner = enabled ? _ => Task.FromResult(0) : null;

        var exitCode = await harness.InvokeAsync(
            ["__unknown_group__", "--name", SensitiveValue], ErrorText, context, runAgent: runner);

        Assert.Equal(1, exitCode);
        var persisted = AssertPublishedAgentRecord(harness,
            enabled ? "CLI.Activity.AiRecovery" : "CLI.Activity.AiRecoverySkipped",
            "__unknown_group__", "Global");
        Assert.Equal(context.InvocationId, StringTag(persisted, TelemetryLayerTags.InvocationId));
    }

    [Fact]
    public async Task DisabledFallback_DoesNotEmitActivities_WhenTelemetryIsDisabled()
    {
        using var harness = new Harness(telemetryEnabled: false);

        var exitCode = await harness.InvokeAsync(["vm", "lst"], ErrorText);

        Assert.Equal(1, exitCode);
        Assert.Empty(harness.Exported);
    }

    [Fact]
    public async Task DisabledFallback_DoesNotEmitActivities_WhenTelemetryIsUnavailable()
    {
        using var harness = new Harness();

        var exitCode = await Program.TryAgentFallbackAsync(
            () => harness.Options, null, ["vm", "lst"], ErrorText,
            new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), true));

        Assert.Equal(1, exitCode);
        Assert.Empty(harness.Exported);
    }

    [Fact]
    public async Task UnreadableConfiguration_DoesNotEmitDisabledSkip()
    {
        using var harness = new Harness();

        var exitCode = await harness.InvokeAsync(
            ["vm", "lst"], ErrorText,
            getOptions: () => throw new InvalidOperationException("test-only configuration failure"));

        Assert.Equal(1, exitCode);
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
            var exitCode = await harness.InvokeAsync(["vm", "lst"], ErrorText);

            Assert.Equal(1, exitCode);
            Assert.Empty(harness.Exported);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE", previous);
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(130, 130)]
    public async Task Oneshot_RecordsActualAgentExitWithoutMaskingTheOriginalCommandFailure(
        int agentExitCode, int expectedCommandExitCode)
    {
        using var harness = new Harness();
        harness.Options.Enabled = true;
        var context = new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), true,
            "11111111111111111111111111111111", "2222222222222222", "vm show", DataBoundary: "EU");
        var launched = false;

        var result = await harness.InvokeAsync(["vm", "show"], ErrorText, context, runAgent: startInfo =>
        {
            launched = true;
            Assert.True(TelemetryLayerEnvironment.TryReadAgentLaunch(
                key => startInfo.Environment.TryGetValue(key, out var value) ? value : null,
                out var childContext, out var fileId, out var diagnostic));
            Assert.Equal(context, childContext);
            Assert.Equal(context.InvocationId, fileId);
            Assert.Null(diagnostic);
            Assert.False(startInfo.Environment.ContainsKey("AZCLIPS_RECOVERY_TRACEPARENT"));
            return Task.FromResult(agentExitCode);
        });

        Assert.True(launched);
        Assert.Equal(expectedCommandExitCode, result);
        var recovery = Assert.Single(harness.Exported);
        Assert.Equal("CLI.Activity.AiRecovery", recovery.Name);
        Assert.Equal(agentExitCode, recovery.Tags[TelemetryTags.RecoveryExitCode]);
        Assert.Equal("Agent", recovery.Tags[TelemetryLayerTags.ServiceName]);
        Assert.Equal("EU", recovery.Tags[TelemetryTags.DataBoundary]);
        Assert.Equal(context.InvocationId, recovery.Tags[TelemetryLayerTags.InvocationId]);
        Assert.Equal(default, recovery.ParentSpanId);
        var persisted = AssertPublishedAgentRecord(
            harness, "CLI.Activity.AiRecovery", "vm show", "EU");
        Assert.Equal(context.CommandTraceId, StringTag(persisted, TelemetryTags.CommandTraceId));
        Assert.Equal(context.CommandSpanId, StringTag(persisted, TelemetryTags.CommandSpanId));
        Assert.Equal(context.InvocationId, StringTag(persisted, TelemetryLayerTags.InvocationId));
        Assert.Equal(agentExitCode, persisted[TelemetryTags.RecoveryExitCode].GetInt32());
    }

    [Fact]
    public async Task SpawnFailure_StillEndsTheAgentInvocationRecord()
    {
        using var harness = new Harness();
        harness.Options.Enabled = true;

        var exitCode = await harness.InvokeAsync(
            ["vm", "show"], ErrorText,
            runAgent: _ => throw new InvalidOperationException("fake spawn failure"));

        Assert.Equal(1, exitCode);
        var record = Assert.Single(harness.Exported);
        Assert.Equal("CLI.Activity.AiRecovery", record.Name);
        Assert.Equal(ActivityStatusCode.Error, record.Status);
        Assert.Equal("Agent", record.Tags[TelemetryLayerTags.ServiceName]);
    }

    private static Dictionary<string, JsonElement> AssertPublishedAgentRecord(
        Harness harness, string expectedName, string? expectedCommand, string expectedBoundary)
    {
        var record = harness.ReadSinglePublishedRecord();
        Assert.Equal(expectedName, record.GetProperty("OperationName").GetString());
        Assert.Equal(string.Empty, record.GetProperty("ParentSpanId").GetString());
        Assert.Equal("Agent", record.GetProperty("Resource").GetProperty("service.name").GetString());
        Assert.Equal(expectedBoundary, record.GetProperty("DataBoundary").GetString());
        var tags = record.GetProperty("Tags").EnumerateArray()
            .ToDictionary(tag => tag.GetProperty("Key").GetString()!, tag => tag.GetProperty("Value").Clone());
        Assert.Equal("Agent", StringTag(tags, TelemetryLayerTags.ServiceName));
        Assert.Equal("agent-v1", StringTag(tags, TelemetryLayerTags.Schema));
        Assert.Equal(expectedBoundary, StringTag(tags, TelemetryTags.DataBoundary));
        Assert.Equal(expectedCommand, StringTag(tags, TelemetryTags.Command));
        if (expectedCommand is null)
            Assert.False(tags.ContainsKey(TelemetryTags.Command));
        Assert.True(Guid.TryParseExact(StringTag(tags, TelemetryLayerTags.InvocationId), "N", out _));
        Assert.DoesNotContain(SensitiveValue, record.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(ErrorText, record.GetRawText(), StringComparison.Ordinal);
        Assert.False(tags.ContainsKey(TelemetryTags.Parameter));
        return tags;
    }

    private static string? StringTag(IReadOnlyDictionary<string, JsonElement> tags, string key) =>
        tags.TryGetValue(key, out var value) ? value.GetString() : null;

    private sealed record ExportedActivity(
        string Name, ActivityTraceId TraceId, ActivitySpanId ParentSpanId,
        ActivityStatusCode Status, string? StatusDescription,
        Dictionary<string, object?> Tags, ActivityEvent[] Events);

    private sealed class Harness : IDisposable
    {
        private readonly TestTelemetryProvider _provider;
        private readonly ActivityListener _agentListener;
        private readonly TelemetryLayerScopeFactory _factory;
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "cli-agent-layer-" + Guid.NewGuid().ToString("N"));
        private readonly bool _enabled;

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
            Telemetry = new TelemetryService(_provider, configuration.Object, ClientType.CLI);
            _enabled = telemetryEnabled;
            _factory = new TelemetryLayerScopeFactory(new TraceDirectory(_directory), configuration.Object);
            _agentListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Agent",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => Exported.Add(new ExportedActivity(
                    activity.OperationName, activity.TraceId, activity.ParentSpanId, activity.Status,
                    activity.StatusDescription, activity.TagObjects.ToDictionary(tag => tag.Key, tag => tag.Value),
                    activity.Events.ToArray())),
            };
            ActivitySource.AddActivityListener(_agentListener);
        }

        public async Task<int> InvokeAsync(
            string[] args,
            string? errorText,
            TelemetryInvocationContext? context = null,
            Func<AgentOptions>? getOptions = null,
            Func<ProcessStartInfo, Task<int>>? runAgent = null)
        {
            context ??= new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), _enabled);
            return await Program.TryAgentFallbackAsync(
                _factory, getOptions ?? (() => Options), args, errorText, context,
                runAgent, runAgent is null ? null : () => Path.Combine(AppContext.BaseDirectory, "fake-agent.exe"));
        }

        public JsonElement ReadSinglePublishedRecord()
        {
            Assert.True(Directory.Exists(_directory));
            var file = Assert.Single(Directory.GetFiles(_directory, "traces-agent-*.log"));
            var content = File.ReadAllText(file);
            var arrayStart = content.IndexOf('[');
            Assert.InRange(arrayStart, 0, content.Length - 1);
            using var batch = JsonDocument.Parse(content[arrayStart..]);
            return Assert.Single(batch.RootElement.EnumerateArray()).Clone();
        }

        public void Dispose()
        {
            Telemetry.Dispose();
            _provider.ActivitySource.Dispose();
            _agentListener.Dispose();
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        private sealed class TraceDirectory(string path) : ITraceDirectoryProvider
        {
            public string GetTraceDirectory() => path;
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
