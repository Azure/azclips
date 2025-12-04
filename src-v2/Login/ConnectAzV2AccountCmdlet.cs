using System.Management.Automation;

namespace Login;

[Cmdlet(VerbsCommunications.Connect, "AzV2Account")]
[OutputType(typeof(string))]
public class ConnectAzV2AccountCmdlet : PSCmdlet
{
    [Parameter(Mandatory = false, ValueFromPipelineByPropertyName = true)]
    public string? TenantId { get; set; }

    [Parameter(Mandatory = false, ValueFromPipelineByPropertyName = true)]
    public string? SubscriptionId { get; set; }

    protected override void ProcessRecord()
    {
        var args = new List<string> { "login" };
        
        if (!string.IsNullOrEmpty(TenantId))
        {
            args.AddRange(new[] { "--tenant", TenantId });
        }

        if (!string.IsNullOrEmpty(SubscriptionId))
        {
            args.AddRange(new[] { "--subscription", SubscriptionId });
        }

        try
        {
            CliRunner.ExecuteCommand(args.ToArray());
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "ConnectAzV2AccountError", ErrorCategory.AuthenticationError, null));
        }
    }
}
