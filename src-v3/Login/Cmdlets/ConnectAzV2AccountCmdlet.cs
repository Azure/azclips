using System.Management.Automation;
using Login.Business;
using Login.Business.Models;

namespace Login.Cmdlets;

/// <summary>
/// PowerShell adapter for Azure authentication.
/// Translates PowerShell parameters to business request.
/// </summary>
[Cmdlet(VerbsCommunications.Connect, "AzV3Account")]
[OutputType(typeof(AuthenticationInfo))]
public class ConnectAzV3AccountCmdlet : PSCmdlet
{
    [Parameter(Mandatory = false, ValueFromPipelineByPropertyName = true)]
    public string? TenantId { get; set; }

    [Parameter(Mandatory = false, ValueFromPipelineByPropertyName = true)]
    public string? SubscriptionId { get; set; }

    protected override void ProcessRecord()
    {
        var request = new LoginRequest
        {
            TenantId = TenantId,
            SubscriptionId = SubscriptionId
        };

        try
        {
            WriteVerbose("Authenticating...");
            
            // Call business logic (same as CLI!)
            var result = AuthenticationManager.LoginAsync(request, CancellationToken.None).GetAwaiter().GetResult();
            
            WriteVerbose("Authentication successful!");
            WriteObject(result);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "ConnectAzV3AccountError", ErrorCategory.AuthenticationError, null));
        }
    }
}
