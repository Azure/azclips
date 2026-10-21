// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Agent.Hosting;
using Core.Agent.Tools.Help;
using Core.Agent.Tools.Resource.Cache;
using Microsoft.Extensions.AI;
using Xunit;
using CliHelp = Core.Cli.Help;

namespace Core.Agent.Tests;

public sealed class HelpToolsTelemetryTests
{
    [Fact]
    public void EveryToolSpan_RecordsItsName()
    {
        using var capture = new SpanCapture();
        using (ToolInvocationLog.Start("find_resources", activityCreator: capture.Start)) { }

        Assert.Equal("find_resources", capture.Single.GetTagItem(TelemetryTags.AgentToolName));
    }

    [Theory]
    [InlineData("vm create", "vm create")]
    [InlineData("VM CREATE", "vm create")]
    [InlineData("new-azvm", "New-AzVM")]
    public async Task GetCommandHit_RecordsTheCatalogNameInTheFormThatWasRequested(string requested, string expected)
    {
        var span = await InvokeGetCommandAsync(requested, VmCreateCatalog());

        Assert.Equal(expected, span.GetTagItem(TelemetryTags.AgentToolRequest));
        Assert.Equal("found", span.GetTagItem(TelemetryTags.AgentToolResponse));
    }

    [Fact]
    public async Task GetCommandHit_WithoutCliName_FallsBackToThePowerShellName()
    {
        var span = await InvokeGetCommandAsync("get-azthing",
            [new("thing", "1.0.0", "thing", [Command("", "Get-AzThing")])]);

        Assert.Equal("Get-AzThing", span.GetTagItem(TelemetryTags.AgentToolRequest));
    }

    [Fact]
    public async Task GetCommandMiss_NeverRecordsTheRequestedText()
    {
        var span = await InvokeGetCommandAsync("vm lst --password hunter2", VmCreateCatalog());

        Assert.Equal("(not found)", span.GetTagItem(TelemetryTags.AgentToolRequest));
        Assert.Equal("not_found", span.GetTagItem(TelemetryTags.AgentToolResponse));
        Assert.DoesNotContain(span.TagObjects, tag => tag.Value is string text && text.Contains("hunter2"));
    }

    [Fact]
    public async Task GetCommandCatalogFailure_RecordsErrorWithoutTheRequestedText()
    {
        using var fixture = new HelpCatalogFixture();
        var provider = new HelpCatalogProvider(fixture.Executable, fixture.Configuration, fixture.CacheDirectory,
            _ => Task.FromResult("""{"error":"az_exit_1","stderr":"boom"}"""));
        using var trace = new AgentTraceCapture();

        await new HelpTools(provider, trace.StartToolActivity).GetCommand
            .InvokeAsync(new AIFunctionArguments { ["name"] = "vm lst --password hunter2" });

        var span = Assert.Single(trace.Stopped, activity => activity.OperationName == "tool.get_command");
        Assert.Equal("(not found)", span.GetTagItem(TelemetryTags.AgentToolRequest));
        Assert.Equal("error", span.GetTagItem(TelemetryTags.AgentToolResponse));
        Assert.DoesNotContain(span.TagObjects, tag => tag.Value is string text && text.Contains("hunter2"));
    }

    [Fact]
    public async Task GetCommandCancelled_NeverRecordsTheRequestedText()
    {
        using var fixture = new HelpCatalogFixture();
        using var cts = new CancellationTokenSource();
        var provider = new HelpCatalogProvider(fixture.Executable, fixture.Configuration, fixture.CacheDirectory,
            _ => { cts.Cancel(); throw new OperationCanceledException(cts.Token); });
        using var trace = new AgentTraceCapture();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new HelpTools(provider, trace.StartToolActivity).GetCommand
            .InvokeAsync(new AIFunctionArguments { ["name"] = "vm lst --password hunter2" }, cts.Token).AsTask());

        AssertNoRequestedText(trace);
    }

    [Fact]
    public async Task GetCommandUnexpectedFault_NeverRecordsTheRequestedText()
    {
        using var fixture = new HelpCatalogFixture();
        var provider = new HelpCatalogProvider(fixture.Executable, fixture.Configuration, fixture.CacheDirectory,
            _ => throw new InvalidOperationException("unexpected"));
        using var trace = new AgentTraceCapture();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new HelpTools(provider, trace.StartToolActivity).GetCommand
            .InvokeAsync(new AIFunctionArguments { ["name"] = "vm lst --password hunter2" }).AsTask());

        AssertNoRequestedText(trace);
    }

    [Fact]
    public async Task GetCommand_TagsItsOwnToolSpan()
    {
        var span = await InvokeGetCommandAsync("New-AzVM", VmCreateCatalog());

        Assert.Equal("get_command", span.GetTagItem(TelemetryTags.AgentToolName));
        Assert.Equal("New-AzVM", span.GetTagItem(TelemetryTags.AgentToolRequest));
        Assert.Equal("found", span.GetTagItem(TelemetryTags.AgentToolResponse));
    }

    [Fact]
    public void ToolSpan_DisposedWithoutResponse_RecordsError()
    {
        using var capture = new SpanCapture();
        using (var scope = ToolInvocationLog.Start("find_resources", activityCreator: capture.Start))
            scope.RecordRequest("type=vm");

        Assert.Equal("error", capture.Single.GetTagItem(TelemetryTags.AgentToolResponse));
    }

    [Fact]
    public async Task ListCommands_RecordsTheFixedRequest()
    {
        using var fixture = new HelpCatalogFixture();
        fixture.WriteCatalog(manifests: VmCreateCatalog());
        using var trace = new AgentTraceCapture();

        await new HelpTools(fixture.Provider, trace.StartToolActivity).ListCommands.InvokeAsync(new AIFunctionArguments());

        var span = Assert.Single(trace.Stopped, activity => activity.OperationName == "tool.list_commands");
        Assert.Equal("list_commands", span.GetTagItem(TelemetryTags.AgentToolName));
        Assert.Equal("full catalog", span.GetTagItem(TelemetryTags.AgentToolRequest));
        Assert.Equal("ok", span.GetTagItem(TelemetryTags.AgentToolResponse));
    }

    [Theory]
    [InlineData("completed", "status=completed")]
    [InlineData("OUT_OF_SCOPE_secret-value", "status=invalid")]
    public async Task SubmitResponse_RecordsOnlyTheClosedStatus(string status, string expected)
    {
        using var trace = new AgentTraceCapture();
        var tool = new SubmitResponseTool(activityCreator: trace.StartToolActivity).SubmitResponse;

        await tool.InvokeAsync(new AIFunctionArguments { ["status"] = status, ["message"] = "private detail" });

        var span = Assert.Single(trace.Stopped, activity => activity.OperationName == "tool.submit_response");
        Assert.Equal(expected, span.GetTagItem(TelemetryTags.AgentToolRequest));
        Assert.DoesNotContain(span.TagObjects, tag => tag.Value is string text && text.Contains("private detail"));
    }

    private static async Task<Activity> InvokeGetCommandAsync(string name, IList<CliHelp.HelpManifest> manifests)
    {
        using var fixture = new HelpCatalogFixture();
        fixture.WriteCatalog(manifests: manifests);
        using var trace = new AgentTraceCapture();

        await new HelpTools(fixture.Provider, trace.StartToolActivity).GetCommand
            .InvokeAsync(new AIFunctionArguments { ["name"] = name });

        return Assert.Single(trace.Stopped, activity => activity.OperationName == "tool.get_command");
    }

    private static void AssertNoRequestedText(AgentTraceCapture trace)
    {
        var span = Assert.Single(trace.Stopped, activity => activity.OperationName == "tool.get_command");
        Assert.Equal("(not found)", span.GetTagItem(TelemetryTags.AgentToolRequest));
        Assert.Equal("error", span.GetTagItem(TelemetryTags.AgentToolResponse));
        Assert.DoesNotContain(span.TagObjects, tag => tag.Value is string text && text.Contains("hunter2"));
    }

    private static IList<CliHelp.HelpManifest> VmCreateCatalog() =>
        [new("compute", "1.0.0", "compute", [Command("vm create", "New-AzVM")])];

    private static CliHelp.HelpCommand Command(string cliName, string psName) =>
        new(cliName, psName, "summary", "description", [], new([], []), []);

    private sealed class SpanCapture : IDisposable
    {
        private readonly ActivitySource _source = new("HelpToolsTelemetryTests." + Guid.NewGuid().ToString("N"));
        private readonly ActivityListener _listener;
        private readonly List<Activity> _started = [];

        public SpanCapture()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source == _source,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public Activity Single => Assert.Single(_started);

        public IDisposable? Start(string name)
        {
            var activity = _source.StartActivity(name);
            if (activity is not null)
                _started.Add(activity);
            return activity;
        }

        public void Dispose()
        {
            _listener.Dispose();
            _source.Dispose();
        }
    }
}
