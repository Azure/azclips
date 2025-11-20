using System.Management.Automation;

namespace Group;

[Cmdlet(VerbsCommon.Get, "CirrusGroup")]
public class GroupGetCmdlet : Cmdlet
{
    // todo: add parameters

    protected override void ProcessRecord()
    {
        var command = new GroupGetCommand(); // todo: pass parameters
        command.ExecuteAsync().GetAwaiter().GetResult();

        // todo: handle output (typed), errors, etc.
    }
}
