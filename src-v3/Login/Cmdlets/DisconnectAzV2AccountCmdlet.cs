using System.Management.Automation;
using Login.Business;

namespace Login.Cmdlets;

/// <summary>
/// PowerShell adapter for logging out.
/// </summary>
[Cmdlet(VerbsCommunications.Disconnect, "AzV3Account")]
[OutputType(typeof(void))]
public class DisconnectAzV3AccountCmdlet : PSCmdlet
{
    protected override void ProcessRecord()
    {
        try
        {
            // Call business logic (same as CLI!)
            AuthenticationManager.Logout();
            WriteVerbose("Logged out successfully.");
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "DisconnectAzV3AccountError", ErrorCategory.AuthenticationError, null));
        }
    }
}
