// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Runtime.Recovery;
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
    public async Task StartupReservation_AlreadyProtectsTheFileBeforeCommandFailure()
    {
        var home = Path.Combine(Path.GetTempPath(), "cli-reservation-" + Guid.NewGuid().ToString("N"));
        var previous = Environment.GetEnvironmentVariable(HomePaths.HomeOverrideVariable);
        Environment.SetEnvironmentVariable(HomePaths.HomeOverrideVariable, home);
        try
        {
            using var harness = new Harness(traceDirectory: HomePaths.GetDefaultTraceDirectory());
            using var command = harness.Telemetry.StartRootActivity();
            Assert.NotNull(command);
            var invocationId = Program.BeginCommandUploadReservation(command, true);
            Assert.NotNull(invocationId);
            using (harness.Telemetry.StartActivity("AzureCLI.ParseGlobalArgs")) { }

            var marker = Assert.Single(Directory.GetFiles(HomePaths.GetDefaultTraceDirectory(), "traces-hold-*.json"));
            using (var document = JsonDocument.Parse(File.ReadAllText(marker)))
                Assert.False(document.RootElement.GetProperty("Ready").GetBoolean());
            Assert.Single(Directory.GetFiles(HomePaths.GetDefaultTraceDirectory(), "traces-*.log"));
            Assert.False(command.IsStopped);

            await Program.StopRootThenFallbackAsync(command, true, context =>
            {
                Assert.Equal(invocationId, context.InvocationId);
                Assert.True(command.IsStopped);
                using var document = JsonDocument.Parse(File.ReadAllText(marker));
                Assert.False(document.RootElement.GetProperty("Ready").GetBoolean());
                return Task.FromResult(1);
            }, invocationId);
            TelemetryUploadReservation.MarkReady(invocationId);
            using var ready = JsonDocument.Parse(File.ReadAllText(marker));
            Assert.True(ready.RootElement.GetProperty("Ready").GetBoolean());
        }
        finally
        {
            Environment.SetEnvironmentVariable(HomePaths.HomeOverrideVariable, previous);
            if (Directory.Exists(home))
                Directory.Delete(home, recursive: true);
        }
    }

    [Theory]
    [InlineData("EU")]
    [InlineData("Global")]
    public async Task RecoverySpan_PreservesCapturedRoutingInTheCliFile(string boundary)
    {
        using var harness = new Harness();
        harness.Options.Recovery.Trigger = "auto";
        var command = harness.Telemetry.StartRootActivity();
        Assert.NotNull(command);
        command.SetTag(TelemetryTags.CommandName, "vm show");
        command.SetTag(TelemetryTags.DataBoundary, boundary);
        command.SetTag(TelemetryTags.TenantId, "tenant-test");
        command.SetTag(TelemetryTags.SubscriptionId, "subscription-test");

        var exitCode = await Program.StopRootThenFallbackAsync(
            command, true,
            context => harness.InvokeAsync(["vm", "show"], ErrorText, context,
                runAgent: startInfo =>
                {
                    Assert.True(TelemetryLayerEnvironment.TryReadAgentLaunch(
                        key => startInfo.Environment.TryGetValue(key, out var value) ? value : null,
                        out var launched, out _, out _));
                    Assert.NotNull(launched);
                    Assert.Equal(boundary, launched.DataBoundary);
                    Assert.Equal("tenant-test", launched.TenantId);
                    Assert.Equal("subscription-test", launched.SubscriptionId);
                    return Task.FromResult(0);
                }));

        Assert.Equal(1, exitCode);
        var recovery = Assert.Single(harness.Exported, item => item.Name == "CLI.Activity.AiRecovery");
        Assert.Equal(command.TraceId, recovery.TraceId);
        Assert.Equal(command.SpanId, recovery.ParentSpanId);
        var tags = AssertPersistedCliRecord(harness, "CLI.Activity.AiRecovery", "vm show", boundary);
        Assert.Equal("tenant-test", StringTag(tags, TelemetryTags.TenantId));
        Assert.Equal("subscription-test", StringTag(tags, TelemetryTags.SubscriptionId));
        Assert.Equal("tenant-test", harness.ReadLastPersistedRecord().GetProperty("tenantId").GetString());
    }

    [Fact]
    public async Task RecoverySpan_IsACommandChild_AndPassesItsContextToAgent()
    {
        using var harness = new Harness();
        harness.Options.Recovery.Trigger = "auto";
        var context = new TelemetryInvocationContext(
            Guid.NewGuid().ToString("N"),
            true,
            "11111111111111111111111111111111",
            "2222222222222222",
            "vm show",
            CommandTraceParent: "00-11111111111111111111111111111111-2222222222222222-01");
        TelemetryInvocationContext? launchedContext = null;

        var exitCode = await Program.TryAgentFallbackAsync(
            () => harness.Options,
            harness.Telemetry,
            ["vm", "show"],
            ErrorText,
            context,
            runAgent: Harness.ReadLaunch(startInfo =>
            {
                Assert.True(TelemetryLayerEnvironment.TryReadAgentLaunch(
                    key => startInfo.Environment.TryGetValue(key, out var value) ? value : null,
                    out launchedContext, out _, out var diagnostic));
                Assert.Null(diagnostic);
                return Task.FromResult(0);
            }),
            findAgent: () => Path.Combine(AppContext.BaseDirectory, "fake-agent.exe"));

        Assert.Equal(1, exitCode);
        var recovery = Assert.Single(harness.Exported);
        Assert.Equal("CLI.Activity.AiRecovery", recovery.Name);
        Assert.Equal(ActivityTraceId.CreateFromString(context.CommandTraceId.AsSpan()), recovery.TraceId);
        Assert.Equal(ActivitySpanId.CreateFromString(context.CommandSpanId.AsSpan()), recovery.ParentSpanId);
        Assert.NotNull(launchedContext);
        Assert.True(launchedContext.TryGetRecoveryParentContext(out var sessionParent));
        Assert.Equal(recovery.TraceId, sessionParent.TraceId);
        Assert.Equal(recovery.SpanId, sessionParent.SpanId);
    }

    [Fact]
    public async Task DisabledFallback_EmitsSeparateRoot_WithInstallationIdAndCommandCorrelation()
    {
        using var harness = new Harness();
        var command = harness.Telemetry.StartRootActivity();
        Assert.NotNull(command);
        command.SetTag(TelemetryTags.CommandName, "profile show");
        command.SetTag(TelemetryTags.DataBoundary, "EU");
        command.SetTag(TelemetryTags.TenantId, "tenant-eu");
        command.SetTag(TelemetryTags.SubscriptionId, "subscription-eu");

        var exitCode = await Program.StopRootThenFallbackAsync(
            command, true,
            context => harness.InvokeAsync(
                ["profile", "show", SensitiveValue, "--token", SensitiveValue],
                ErrorText, context));

        Assert.Null(exitCode);
        Assert.Equal(new[] { "CLI.Activity.Root", "CLI.Activity.AiRecoverySkipped" },
            harness.Exported.Select(activity => activity.Name));
        Assert.DoesNotContain(harness.Exported, activity => activity.Name == "CLI.Activity.AiRecovery");
        var skipped = harness.Exported[1];
        Assert.NotEqual(command.TraceId, skipped.TraceId);
        Assert.Equal(default, skipped.ParentSpanId);
        Assert.Equal("disabled", skipped.Tags.GetValueOrDefault(TelemetryTags.RecoverySkipReason));
        Assert.Equal("profile show", skipped.Tags.GetValueOrDefault(TelemetryTags.CommandName));
        Assert.Equal(command.TraceId.ToString(), skipped.Tags.GetValueOrDefault(TelemetryTags.CommandTraceId));
        Assert.Equal(command.SpanId.ToString(), skipped.Tags.GetValueOrDefault(TelemetryTags.CommandSpanId));
        Assert.Equal(TelemetryPropertyResolver.InstallationId,
            skipped.Tags.GetValueOrDefault(TelemetryTags.InstallationId));
        Assert.False(string.IsNullOrEmpty(skipped.Tags.GetValueOrDefault(TelemetryTags.InstallationId) as string));
        Assert.Equal("CLI", skipped.Tags.GetValueOrDefault(TelemetryTags.ClientType));
        Assert.False(skipped.Tags.ContainsKey(TelemetryLayerTags.ServiceName));
        Assert.False(skipped.Tags.ContainsKey(TelemetryLayerTags.Schema));
        Assert.Equal("CLI.Activity.AiRecoverySkipped", skipped.Tags.GetValueOrDefault(TelemetryTags.EventName));
        Assert.Equal(ActivityStatusCode.Unset, skipped.Status);
        Assert.Null(skipped.StatusDescription);
        Assert.Empty(skipped.Events);
        Assert.False(skipped.Tags.ContainsKey(TelemetryTags.CommandParameter));
        Assert.False(skipped.Tags.ContainsKey(TelemetryTags.RecoveryExitCode));
        Assert.DoesNotContain(skipped.Tags.Values, value => value is string text &&
            (text.Contains(SensitiveValue, StringComparison.Ordinal) ||
             text.Contains(ErrorText, StringComparison.Ordinal)));
        var persisted = AssertPersistedCliRecord(
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

        Assert.Null(exitCode);
        var skipped = Assert.Single(harness.Exported);
        Assert.Equal("CLI.Activity.AiRecoverySkipped", skipped.Name);
        Assert.Equal(expectedCommand, skipped.Tags.GetValueOrDefault(TelemetryTags.CommandName));
        Assert.False(skipped.Tags.ContainsKey(TelemetryTags.CommandTraceId));
        Assert.False(skipped.Tags.ContainsKey(TelemetryTags.CommandSpanId));
        Assert.DoesNotContain(skipped.Tags.Values, value => value is string text &&
            text.Contains(SensitiveValue, StringComparison.Ordinal));
        var persisted = AssertPersistedCliRecord(
            harness, "CLI.Activity.AiRecoverySkipped", expectedCommand, "Unknown");
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
        harness.Options.Recovery.Trigger = "auto";
        var context = new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), true,
            "11111111111111111111111111111111", "2222222222222222", DataBoundary: "EU",
            CommandTraceParent: "00-11111111111111111111111111111111-2222222222222222-01");
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
        var persisted = AssertPersistedCliRecord(
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
    public async Task EmptyResolvedCommand_IsFilledBeforeRecording(bool enabled)
    {
        using var harness = new Harness();
        harness.Options.Recovery.Trigger = enabled ? "auto" : "manual";
        var context = new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), true, Command: "");
        Func<ProcessStartInfo, Task<int>>? runner = enabled ? _ => Task.FromResult(0) : null;

        var exitCode = await harness.InvokeAsync(
            ["__unknown_group__", "--name", SensitiveValue], ErrorText, context, runAgent: runner);

        Assert.Equal(enabled ? 1 : null, exitCode);
        var persisted = AssertPersistedCliRecord(harness,
            enabled ? "CLI.Activity.AiRecovery" : "CLI.Activity.AiRecoverySkipped",
            "__unknown_group__", "Unknown");
        Assert.Equal(context.InvocationId, StringTag(persisted, TelemetryLayerTags.InvocationId));
    }

    [Fact]
    public async Task DisabledFallback_DoesNotEmitActivities_WhenTelemetryIsDisabled()
    {
        using var harness = new Harness(telemetryEnabled: false);

        var exitCode = await harness.InvokeAsync(["vm", "lst"], ErrorText);

        Assert.Null(exitCode);
        Assert.Empty(harness.Exported);
    }

    [Fact]
    public async Task InvalidStoredStyle_HandsTheAgentAStyleItsResolveAccepts()
    {
        using var harness = new Harness();
        harness.Options.Recovery.Trigger = "auto";
        harness.Options.Recovery.Style = "one-shot";
        RecoveryRequest? request = null;

        await Program.TryAgentFallbackAsync(
            () => harness.Options, harness.Telemetry, ["vm", "lst"], ErrorText,
            new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), true),
            runAgent: (agentRequest, _, _) => { request = agentRequest; return Task.FromResult(0); },
            findAgent: () => Path.Combine(AppContext.BaseDirectory, "fake-agent.exe"));

        // The Agent resolves the request exactly like this before it starts.
        var settings = RecoverySessionSettings.Resolve(harness.Options, request!.Trigger, request.SessionStyle);
        Assert.Equal("oneshot", settings.SessionStyle);
    }

    [Fact]
    public async Task DisabledFallback_DoesNotEmitActivities_WhenTelemetryIsUnavailable()
    {
        using var harness = new Harness();

        var exitCode = await Program.TryAgentFallbackAsync(
            () => harness.Options, null, ["vm", "lst"], ErrorText,
            new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), true));

        Assert.Null(exitCode);
        Assert.Empty(harness.Exported);
    }

    [Fact]
    public async Task UnreadableConfiguration_DoesNotEmitDisabledSkip()
    {
        using var harness = new Harness();

        var exitCode = await harness.InvokeAsync(
            ["vm", "lst"], ErrorText,
            getOptions: () => throw new InvalidOperationException("test-only configuration failure"));

        Assert.Null(exitCode);
        Assert.Empty(harness.Exported);
    }

    [Fact]
    public async Task EnabledRecursionGuard_DoesNotEmitDisabledSkip()
    {
        using var harness = new Harness();
        harness.Options.Recovery.Trigger = "auto";
        var previous = Environment.GetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE");
        Environment.SetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE", "1");
        try
        {
            var exitCode = await harness.InvokeAsync(["vm", "lst"], ErrorText);

            Assert.Null(exitCode);
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
        harness.Options.Recovery.Trigger = "auto";
        var context = new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), true,
            "11111111111111111111111111111111", "2222222222222222", "vm show", DataBoundary: "EU",
            CommandTraceParent: "00-11111111111111111111111111111111-2222222222222222-01");
        TelemetryInvocationContext? childContext = null;
        var launched = false;

        var result = await harness.InvokeAsync(["vm", "show"], ErrorText, context, runAgent: startInfo =>
        {
            launched = true;
            Assert.True(TelemetryLayerEnvironment.TryReadAgentLaunch(
                key => startInfo.Environment.TryGetValue(key, out var value) ? value : null,
                out childContext, out var fileId, out var diagnostic));
            Assert.Equal(context.InvocationId, fileId);
            Assert.Null(diagnostic);
            Assert.False(startInfo.Environment.ContainsKey("AZCLIPS_RECOVERY_TRACEPARENT"));
            return Task.FromResult(agentExitCode);
        });

        Assert.True(launched);
        Assert.Equal(expectedCommandExitCode, result);
        var recovery = Assert.Single(harness.Exported);
        Assert.Equal("CLI.Activity.AiRecovery", recovery.Name);
        Assert.Equal(ActivityTraceId.CreateFromString(context.CommandTraceId!.AsSpan()), recovery.TraceId);
        Assert.Equal(ActivitySpanId.CreateFromString(context.CommandSpanId!.AsSpan()), recovery.ParentSpanId);
        Assert.NotNull(childContext);
        Assert.True(childContext.TryGetRecoveryParentContext(out var agentSessionParent));
        Assert.Equal(recovery.TraceId, agentSessionParent.TraceId);
        Assert.Equal(recovery.SpanId, agentSessionParent.SpanId);
        Assert.Equal(agentExitCode, recovery.Tags[TelemetryTags.RecoveryExitCode]);
        Assert.False(recovery.Tags.ContainsKey(TelemetryLayerTags.ServiceName));
        Assert.Equal("EU", recovery.Tags[TelemetryTags.DataBoundary]);
        Assert.Equal(context.InvocationId, recovery.Tags[TelemetryLayerTags.InvocationId]);
        var persisted = AssertPersistedCliRecord(
            harness, "CLI.Activity.AiRecovery", "vm show", "EU");
        Assert.Equal(context.CommandTraceId, StringTag(persisted, TelemetryTags.CommandTraceId));
        Assert.Equal(context.CommandSpanId, StringTag(persisted, TelemetryTags.CommandSpanId));
        Assert.Equal(context.InvocationId, StringTag(persisted, TelemetryLayerTags.InvocationId));
        Assert.Equal(agentExitCode, persisted[TelemetryTags.RecoveryExitCode].GetInt32());
    }

    [Fact]
    public async Task SpawnFailure_StillEndsTheCliRecoveryRecord()
    {
        using var harness = new Harness();
        harness.Options.Recovery.Trigger = "auto";

        var exitCode = await harness.InvokeAsync(
            ["vm", "show"], ErrorText,
            runAgent: _ => throw new InvalidOperationException("fake spawn failure"));

        Assert.Null(exitCode);
        var record = Assert.Single(harness.Exported);
        Assert.Equal("CLI.Activity.AiRecovery", record.Name);
        Assert.Equal(ActivityStatusCode.Error, record.Status);
        Assert.False(record.Tags.ContainsKey(TelemetryLayerTags.ServiceName));
    }

    private static Dictionary<string, JsonElement> AssertPersistedCliRecord(
        Harness harness, string expectedName, string? expectedCommand, string expectedBoundary)
    {
        var record = harness.ReadLastPersistedRecord();
        Assert.Equal(expectedName, record.GetProperty("operationName").GetString());
        Assert.Equal(harness.SourceName, record.GetProperty("resource").GetProperty("service.name").GetString());
        Assert.Equal(expectedBoundary, record.GetProperty("dataBoundary").GetString());
        var tags = record.GetProperty("tags").EnumerateArray()
            .ToDictionary(tag => tag.GetProperty("Key").GetString()!, tag => tag.GetProperty("Value").Clone());
        Assert.False(tags.ContainsKey(TelemetryLayerTags.ServiceName));
        Assert.False(tags.ContainsKey(TelemetryLayerTags.Schema));
        Assert.Equal(expectedBoundary, StringTag(tags, TelemetryTags.DataBoundary));
        Assert.Equal(expectedCommand, StringTag(tags, TelemetryTags.CommandName));
        if (expectedCommand is null)
            Assert.False(tags.ContainsKey(TelemetryTags.CommandName));
        Assert.True(Guid.TryParseExact(StringTag(tags, TelemetryLayerTags.InvocationId), "N", out _));
        Assert.DoesNotContain(SensitiveValue, record.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(ErrorText, record.GetRawText(), StringComparison.Ordinal);
        Assert.False(tags.ContainsKey(TelemetryTags.CommandParameter));
        return tags;
    }

    private static string? StringTag(IReadOnlyDictionary<string, JsonElement> tags, string key) =>
        tags.TryGetValue(key, out var value) ? value.GetString() : null;

    private sealed record ExportedActivity(
        string Name, ActivityTraceId TraceId, ActivitySpanId SpanId, ActivitySpanId ParentSpanId,
        ActivityStatusCode Status, string? StatusDescription,
        Dictionary<string, object?> Tags, ActivityEvent[] Events);

    private sealed class Harness : IDisposable
    {
        private readonly TestTelemetryProvider _provider;
        private readonly string _directory;
        private readonly bool _enabled;

        public AgentOptions Options { get; } = new() { Recovery = new() { Trigger = "manual", Style = "oneshot" } };
        public List<ExportedActivity> Exported { get; } = [];
        public TelemetryService Telemetry { get; }
        public string SourceName => _provider.ActivitySource.Name;

        public Harness(bool telemetryEnabled = true, string? traceDirectory = null)
        {
            _directory = traceDirectory ??
                Path.Combine(Path.GetTempPath(), "cli-recovery-" + Guid.NewGuid().ToString("N"));
            var configuration = new Mock<IConfigurationService>();
            configuration.Setup(config => config.GetConfiguration<TelemetryOptions>(
                    It.IsAny<string?>(), It.IsAny<TelemetryOptions?>()))
                .Returns(new TelemetryOptions { Enabled = telemetryEnabled });
            _provider = new TestTelemetryProvider(Exported, new TraceDirectory(_directory));
            Telemetry = new TelemetryService(_provider, configuration.Object, new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));
            _enabled = telemetryEnabled;
        }

        public async Task<int?> InvokeAsync(
            string[] args,
            string? errorText,
            TelemetryInvocationContext? context = null,
            Func<AgentOptions>? getOptions = null,
            Func<ProcessStartInfo, Task<int>>? runAgent = null)
        {
            context ??= new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), _enabled);
            return await Program.TryAgentFallbackAsync(
                getOptions ?? (() => Options), Telemetry, args, errorText, context,
                runAgent: runAgent is null ? null : ReadLaunch(runAgent),
            findAgent: runAgent is null ? null : () => Path.Combine(AppContext.BaseDirectory, "fake-agent.exe"));
        }

        public static Func<RecoveryRequest, TelemetryInvocationContext, CancellationToken, Task<int>> ReadLaunch(
            Func<ProcessStartInfo, Task<int>> runAgent) =>
            (_, context, _) => runAgent(AgentLauncher.BuildAgentStartInfo(
                Path.Combine(AppContext.BaseDirectory, "fake-agent.exe"), "0", context, context.InvocationId));

        public JsonElement ReadLastPersistedRecord()
        {
            Assert.True(Directory.Exists(_directory));
            var file = Assert.Single(Directory.GetFiles(_directory, "traces-*.log"));
            Assert.DoesNotContain("traces-agent-", Path.GetFileName(file), StringComparison.Ordinal);
            var content = File.ReadAllText(file);
            var batchStart = content.LastIndexOf("--- Batch at ", StringComparison.Ordinal);
            Assert.True(batchStart >= 0);
            var arrayStart = content.IndexOf('[', batchStart);
            Assert.InRange(arrayStart, 0, content.Length - 1);
            using var batch = JsonDocument.Parse(content[arrayStart..]);
            return Assert.Single(batch.RootElement.EnumerateArray()).Clone();
        }

        public void Dispose()
        {
            Telemetry.Dispose();
            _provider.ActivitySource.Dispose();
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
        public TestTelemetryProvider(List<ExportedActivity> exported, ITraceDirectoryProvider directory)
        {
            ActivitySource = new ActivitySource("cli-skip-test-" + Guid.NewGuid().ToString("N"));
            var builder = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySource.Name)
                .AddProcessor(new DefaultActivityNameFilterProcessor(new DefaultTelemetryActivityNameFilter()))
                .AddProcessor(new DefaultTelemetryPropertyFilterProcessor(new DefaultTelemetryPropertyFilter()))
                .AddProcessor(new SimpleActivityExportProcessor(new SnapshotExporter(exported)));
            new FileTelemetrySender(directory).ConfigureExporter(builder);
            TracerProvider = builder.Build()!;
        }
    }

    private sealed class SnapshotExporter(List<ExportedActivity> exported) : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
            {
                exported.Add(new ExportedActivity(
                    activity.OperationName, activity.TraceId, activity.SpanId, activity.ParentSpanId,
                    activity.Status, activity.StatusDescription,
                    activity.TagObjects.ToDictionary(tag => tag.Key, tag => tag.Value),
                    activity.Events.ToArray()));
            }
            return ExportResult.Success;
        }
    }
}
