// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Agent.Tools.Help;
using Xunit;

namespace Core.Agent.Tests;

public sealed class HelpToolsTelemetryTests
{
    private const string VmCreate = """{"cliName":"vm create","psName":"New-AzVM","verb":"compute","summary":"Create a VM."}""";

    [Theory]
    [InlineData("vm create", "vm create")]
    [InlineData("VM CREATE", "vm create")]
    [InlineData("new-azvm", "New-AzVM")]
    public void Hit_RecordsTheCatalogNameInTheFormThatWasRequested(string requested, string expected)
    {
        using var activity = new Activity("tool.get_command").Start();

        HelpTools.RecordLookup(activity, requested, VmCreate);

        Assert.Equal(expected, activity.GetTagItem(TelemetryTags.ToolCommandName));
        Assert.Equal(true, activity.GetTagItem(TelemetryTags.ToolCommandFound));
    }

    [Fact]
    public void Hit_WithoutCliName_FallsBackToThePowerShellName()
    {
        using var activity = new Activity("tool.get_command").Start();

        HelpTools.RecordLookup(activity, "get-azthing", """{"cliName":"","psName":"Get-AzThing"}""");

        Assert.Equal("Get-AzThing", activity.GetTagItem(TelemetryTags.ToolCommandName));
    }

    [Theory]
    [InlineData("""{"error":"not_found","name":"vm lst --password hunter2","suggestions":["vm list"]}""")]
    [InlineData("""{"error":"az_exit_1","stderr":"boom"}""")]
    [InlineData("not json")]
    [InlineData("""["unexpected"]""")]
    public void Miss_NeverRecordsTheRequestedText(string helpJson)
    {
        using var activity = new Activity("tool.get_command").Start();

        HelpTools.RecordLookup(activity, "vm lst --password hunter2", helpJson);

        Assert.Equal("(not found)", activity.GetTagItem(TelemetryTags.ToolCommandName));
        Assert.Equal(false, activity.GetTagItem(TelemetryTags.ToolCommandFound));
        Assert.DoesNotContain(activity.TagObjects, tag => tag.Value is string text && text.Contains("hunter2"));
    }

    [Fact]
    public void NoActivity_IsANoOp()
        => HelpTools.RecordLookup(null, "vm create", VmCreate);

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

            await tool.InvokeAsync(new Microsoft.Extensions.AI.AIFunctionArguments { ["name"] = "New-AzVM" });

            var span = Assert.Single(trace.Stopped, activity => activity.OperationName == "tool.get_command");
            Assert.Equal("New-AzVM", span.GetTagItem(TelemetryTags.ToolCommandName));
            Assert.Equal(true, span.GetTagItem(TelemetryTags.ToolCommandFound));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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
}
