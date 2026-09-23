using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// The vm create scenario as a user would run it: a script in a real shell, spawning the real
/// az binary per command, the resource group's name flowing into vm create through a shell
/// variable. Both scripts replay the committed recording end to end without credentials.
/// </summary>
[Collection("Scenario Tests")]
public class VmCreateScriptScenarioTests
{
    private static string RecordingPath
        => SessionRecordFiles.GetPath(typeof(VmCreateScenarioTests), "CreatesVirtualMachine");

    [Fact]
    public void PowerShellScript_ReplaysTheWholeRecording()
    {
        var result = ScriptScenarioRunner.RunPlayback("vm-create.ps1", RecordingPath);

        Assert.True(result.ExitCode == 0, $"exit {result.ExitCode}\n{result.StdOut}\n{result.StdErr}");
        Assert.Contains("created scenario-vm in scenario-vm-rg: Succeeded", result.StdOut);
        Assert.Equal(result.EntriesTotal, result.EntriesConsumed);
    }

    [BashFact]
    public void BashScript_ReplaysTheWholeRecording()
    {
        var result = ScriptScenarioRunner.RunPlayback("vm-create.sh", RecordingPath);

        Assert.True(result.ExitCode == 0, $"exit {result.ExitCode}\n{result.StdOut}\n{result.StdErr}");
        Assert.Contains("created scenario-vm in scenario-vm-rg: Succeeded", result.StdOut);
        Assert.Equal(result.EntriesTotal, result.EntriesConsumed);
    }

    [Fact]
    public void ModuleScript_ReplaysTheWholeRecording()
    {
        var result = ScriptScenarioRunner.RunPlayback("vm-create-module.ps1", RecordingPath);

        Assert.True(result.ExitCode == 0, $"exit {result.ExitCode}\n{result.StdOut}\n{result.StdErr}");
        Assert.Contains("created scenario-vm in scenario-vm-rg: Succeeded", result.StdOut);
        Assert.Equal(result.EntriesTotal, result.EntriesConsumed);
    }

    private sealed class BashFactAttribute : FactAttribute
    {
        public BashFactAttribute()
        {
            if (ScriptScenarioRunner.TryResolveBash() is null)
            {
                Skip = "No usable bash on this machine; on Windows the System32 relay needs WSL, only Git's bash works.";
            }
        }
    }
}
