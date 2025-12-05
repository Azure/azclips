using System.Management.Automation;

namespace Group;

[Cmdlet(VerbsCommon.Get, "CirrusGroup")]
public class GroupGetCmdlet : Cmdlet
{
    // todo: [Wrapper/Execution layer] add parameters

    protected override void ProcessRecord()
    {
        var command = new GroupGetCommand(); // todo: [Wrapper/Execution layer] pass parameters
        command.ExecuteAsync().GetAwaiter().GetResult(); // todo: [Execution layer] Using GetAwaiter().GetResult() in a PowerShell cmdlet can cause deadlocks in certain synchronization contexts. Consider using async/await properly or handling the task differently. PowerShell cmdlets should either use synchronous methods or properly implement async patterns with ProcessRecordAsync in PowerShell 7+.

        // todo: [Wrapper/Execution layer] handle output (typed), errors, etc.
    }
}
