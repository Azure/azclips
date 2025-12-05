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
        var options = new Dictionary<string, string>();
        
        if (!string.IsNullOrEmpty(TenantId))
        {
            options["tenant"] = TenantId;
        }

        if (!string.IsNullOrEmpty(SubscriptionId))
        {
            options["subscription"] = SubscriptionId;
        }

        try
        {
            var command = new LoginCommand();
            command.ExecuteAsync(options, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "ConnectAzV2AccountError", ErrorCategory.AuthenticationError, null));
        }
    }
}
