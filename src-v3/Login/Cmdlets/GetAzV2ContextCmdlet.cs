using System.Management.Automation;
using Login.Business;
using Login.Business.Models;

namespace Login.Cmdlets;

/// <summary>
/// PowerShell adapter for getting authentication context.
/// </summary>
[Cmdlet(VerbsCommon.Get, "AzV3Context")]
[OutputType(typeof(AuthenticationInfo))]
public class GetAzV3ContextCmdlet : PSCmdlet
{
    protected override void ProcessRecord()
    {
        try
        {
            // Call business logic (same as CLI!)
            var context = AuthenticationManager.GetCurrentContext();
            
            if (context == null)
            {
                WriteWarning("Not authenticated. Run 'Connect-AzV3Account' first.");
                return;
            }

            WriteObject(context);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "GetAzV3ContextError", ErrorCategory.AuthenticationError, null));
        }
    }
}
