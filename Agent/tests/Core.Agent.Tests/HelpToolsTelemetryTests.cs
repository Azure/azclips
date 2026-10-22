// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Agent.Hosting;
using Core.Agent.Tools.Help;
using Core.Agent.Tools.Resource.Cache;
using Microsoft.Extensions.AI;
using Xunit;

namespace Core.Agent.Tests;

public sealed class HelpToolsTelemetryTests
{
    private const string VmCreate = """{"cliName":"vm create","psName":"New-AzVM","verb":"compute","summary":"Create a VM."}""";

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
    public void GetCommandHit_RecordsTheCatalogNameInTheFormThatWasRequested(string requested, string expected)
    {
        using var capture = new SpanCapture();
        using (var scope = ToolInvocationLog.Start("get_command", activityCreator: capture.Start))
            HelpTools.RecordLookup(scope, requested, VmCreate);

        Assert.Equal(expected, capture.Single.GetTagItem(TelemetryTags.AgentToolRequest));
        Assert.Equal("found", capture.Single.GetTagItem(TelemetryTags.AgentToolResponse));
    }

    [Fact]
    public void GetCommandHit_WithoutCliName_FallsBackToThePowerShellName()
    {
        using var capture = new SpanCapture();
        using (var scope = ToolInvocationLog.Start("get_command", activityCreator: capture.Start))
            HelpTools.RecordLookup(scope, "get-azthing", """{"cliName":"","psName":"Get-AzThing"}""");

        Assert.Equal("Get-AzThing", capture.Single.GetTagItem(TelemetryTags.AgentToolRequest));
    }

    [Theory]
    [InlineData("""{"error":"not_found","name":"vm lst --password hunter2","suggestions":["vm list"]}""", "not_found")]
    [InlineData("""{"error":"az_exit_1","stderr":"boom"}""", "error")]
    [InlineData("not json", "error")]
    [InlineData("""["unexpected"]""", "not_found")]
    public void GetCommandMiss_NeverRecordsTheRequestedText(string helpJson, string response)
    {
        using var capture = new SpanCapture();
        using (var scope = ToolInvocationLog.Start("get_command", activityCreator: capture.Start))
            HelpTools.RecordLookup(scope, "vm lst --password hunter2", helpJson);

        Assert.Equal("(not found)", capture.Single.GetTagItem(TelemetryTags.AgentToolRequest));
        Assert.Equal(response, capture.Single.GetTagItem(TelemetryTags.AgentToolResponse));
        Assert.DoesNotContain(capture.Single.TagObjects, tag => tag.Value is string text && text.Contains("hunter2"));
    }

    [Theory]
    [InlineData("vm create", "vm create")]
    [InlineData("  vm   create  --admin-password hunter2", "vm create")]
    [InlineData("New-AzVM -Name x", "New-AzVM")]
    [InlineData("--password hunter2", "")]
    [InlineData(null, "")]
    public void CommandPart_KeepsOnlyTheCommandWords(string? name, string expected) =>
        Assert.Equal(expected, HelpTools.CommandPart(name));

    [Fact]
    public void NoTelemetry_IsANoOp()
    {
        using var scope = ToolInvocationLog.Start("get_command");
        HelpTools.RecordLookup(scope, "vm create", VmCreate);
        scope.RecordResponse("found");
    }

    [Fact]
    public async Task GetCommand_TagsItsOwnToolSpan()
    {
        var directory = Path.Combine(Path.GetTempPath(), "help-tools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var fakeAz = WriteFakeAz(directory, VmCreate);
            using var trace = new AgentTraceCapture();
            var tool = new HelpTools(fakeAz, trace.StartToolActivity).GetCommand;

            await tool.InvokeAsync(new AIFunctionArguments { ["name"] = "New-AzVM" });

            var span = Assert.Single(trace.Stopped, activity => activity.OperationName == "tool.get_command");
            Assert.Equal("get_command", span.GetTagItem(TelemetryTags.AgentToolName));
            Assert.Equal("New-AzVM", span.GetTagItem(TelemetryTags.AgentToolRequest));
            Assert.Equal("found", span.GetTagItem(TelemetryTags.AgentToolResponse));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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
        var directory = Path.Combine(Path.GetTempPath(), "help-tools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var fakeAz = WriteFakeAz(directory, "[]");
            using var trace = new AgentTraceCapture();
            var tool = new HelpTools(fakeAz, trace.StartToolActivity).ListCommands;

            await tool.InvokeAsync(new AIFunctionArguments());

            var span = Assert.Single(trace.Stopped, activity => activity.OperationName == "tool.list_commands");
            Assert.Equal("list_commands", span.GetTagItem(TelemetryTags.AgentToolName));
            Assert.Equal("full catalog", span.GetTagItem(TelemetryTags.AgentToolRequest));
            Assert.NotNull(span.GetTagItem(TelemetryTags.AgentToolResponse));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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

    private static string WriteFakeAz(string directory, string stdout)
    {
        if (OperatingSystem.IsWindows())
        {
            var path = Path.Combine(directory, "az.cmd");
            File.WriteAllText(path, "@echo off\r\necho " + stdout + "\r\n");
            return path;
        }
        var script = Path.Combine(directory, "az");
        File.WriteAllText(script, "#!/bin/sh\ncat <<'EOF'\n" + stdout + "\nEOF\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return script;
    }

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
