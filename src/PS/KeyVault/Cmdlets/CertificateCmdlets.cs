using System.Collections;
using System.Globalization;
using System.Management.Automation;
using System.Security;

namespace Microsoft.Azure.Azclips.Cmdlets.KeyVault;

public abstract class CertificateMutationCmdletBase : KeyVaultCmdletBase
{
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string Name { get; set; } = string.Empty;

    [Parameter]
    public bool Enabled { get; set; }

    [Parameter]
    public DateTimeOffset Expires { get; set; }

    [Parameter]
    public DateTimeOffset NotBefore { get; set; }

    [Parameter]
    [Alias("Tags")]
    public Hashtable? Tag { get; set; }

    [Parameter]
    public string? Policy { get; set; }

    [Parameter]
    public string? PolicyPath { get; set; }

    protected Dictionary<string, object> MutationParameters()
    {
        var parameters = new Dictionary<string, object> { ["CertificateName"] = Name };
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Enabled))) parameters.Add("Enabled", Enabled.ToString());
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Expires))) parameters.Add("Expires", Expires.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        if (MyInvocation.BoundParameters.ContainsKey(nameof(NotBefore))) parameters.Add("NotBefore", NotBefore.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        if (Policy is not null) parameters.Add("Policy", Policy);
        if (PolicyPath is not null) parameters.Add("PolicyFile", SessionState.Path.GetUnresolvedProviderPathFromPSPath(PolicyPath));
        if (Tag is not null)
        {
            var tags = new List<string>();
            foreach (DictionaryEntry entry in Tag)
            {
                if (entry.Key is not string key || string.IsNullOrEmpty(key) || key.Contains('='))
                    throw new PSArgumentException("Tag names must be nonempty strings without '='.");
                tags.Add($"{key}={entry.Value}");
            }
            parameters.Add("TagsInput", tags.ToArray());
        }
        return parameters;
    }
}

[Cmdlet(VerbsCommon.Add, "AzKeyVaultCertificate", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.KeyVault.Models.CertificateOperation")]
public class AddAzKeyVaultCertificateCmdlet : CertificateMutationCmdletBase
{
    [Parameter]
    public SwitchParameter NoWait { get; set; }

    [Parameter]
    [ValidateRange(1, 3600)]
    public int TimeoutSeconds { get; set; } = 300;

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"{VaultName ?? VaultUrl}/{Name}", "Create a certificate version")) return;
        var parameters = MutationParameters();
        parameters.Add("NoWait", NoWait.IsPresent);
        parameters.Add("TimeoutSeconds", TimeoutSeconds);
        Execute("Add-AzKeyVaultCertificate", parameters);
    }
}

[Cmdlet(VerbsData.Import, "AzKeyVaultCertificate", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.KeyVault.Models.CertificateBundle")]
public class ImportAzKeyVaultCertificateCmdlet : CertificateMutationCmdletBase
{
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string FilePath { get; set; } = string.Empty;

    [Parameter]
    public SecureString? Password { get; set; }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"{VaultName ?? VaultUrl}/{Name}", "Import a certificate and private key")) return;
        var parameters = MutationParameters();
        parameters.Add("File", SessionState.Path.GetUnresolvedProviderPathFromPSPath(FilePath));
        if (Password is not null) parameters.Add("SecurePassword", Password);
        Execute("Import-AzKeyVaultCertificate", parameters);
    }
}

[Cmdlet(VerbsCommon.Get, "AzKeyVaultCertificate", DefaultParameterSetName = "List")]
[OutputType("Microsoft.Azure.Azclips.Extensions.KeyVault.Models.CertificateBundle", "Microsoft.Azure.Azclips.Extensions.KeyVault.Models.CertificateItem")]
public class GetAzKeyVaultCertificateCmdlet : KeyVaultCmdletBase
{
    [Parameter(Mandatory = true, ParameterSetName = "Get")]
    [Parameter(Mandatory = true, ParameterSetName = "Versions")]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    [Parameter(ParameterSetName = "Get")]
    public string? Version { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Versions")]
    public SwitchParameter IncludeVersions { get; set; }

    [Parameter(ParameterSetName = "List")]
    public bool IncludePending { get; set; }

    [Parameter(ParameterSetName = "List")]
    [Parameter(ParameterSetName = "Versions")]
    [ValidateRange(1, 25)]
    public int MaxResults { get; set; } = 25;

    protected override void ProcessRecord()
    {
        var parameters = new Dictionary<string, object>();
        if (Name is not null) parameters.Add("CertificateName", Name);
        if (Version is not null) parameters.Add("CertificateVersion", Version);
        if (ParameterSetName != "Get" && MyInvocation.BoundParameters.ContainsKey(nameof(MaxResults))) parameters.Add("PageSize", MaxResults);
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IncludePending))) parameters.Add("IncludePendingInput", IncludePending);
        Execute($"Get-AzKeyVaultCertificate_{ParameterSetName}", parameters);
    }
}

[Cmdlet(VerbsData.Update, "AzKeyVaultCertificate", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.KeyVault.Models.CertificateBundle")]
public class UpdateAzKeyVaultCertificateCmdlet : CertificateMutationCmdletBase
{
    [Parameter]
    public string? Version { get; set; }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"{VaultName ?? VaultUrl}/{Name}", "Update certificate attributes or policy")) return;
        var parameters = MutationParameters();
        if (Version is not null) parameters.Add("CertificateVersion", Version);
        Execute("Update-AzKeyVaultCertificate", parameters);
    }
}

[Cmdlet(VerbsData.Export, "AzKeyVaultCertificate", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.KeyVault.Customization.CertificateDownloadResult")]
public class ExportAzKeyVaultCertificateCmdlet : KeyVaultCmdletBase
{
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string Name { get; set; } = string.Empty;

    [Parameter]
    public string? Version { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string FilePath { get; set; } = string.Empty;

    [Parameter]
    [ValidateSet("PEM", "DER")]
    public string Encoding { get; set; } = "PEM";

    protected override void ProcessRecord()
    {
        if (!ShouldProcess(FilePath, "Download public certificate (no private key)")) return;
        var parameters = new Dictionary<string, object>
        {
            ["CertificateName"] = Name,
            ["File"] = SessionState.Path.GetUnresolvedProviderPathFromPSPath(FilePath),
            ["Encoding"] = Encoding
        };
        if (Version is not null) parameters.Add("CertificateVersion", Version);
        Execute("Export-AzKeyVaultCertificate", parameters);
    }
}

[Cmdlet(VerbsCommon.Undo, "AzKeyVaultCertificateRemoval", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.KeyVault.Models.CertificateBundle")]
public class UndoAzKeyVaultCertificateRemovalCmdlet : KeyVaultCmdletBase
{
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string Name { get; set; } = string.Empty;

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"{VaultName ?? VaultUrl}/{Name}", "Recover a soft-deleted certificate and its versions")) return;
        Execute("Undo-AzKeyVaultCertificateRemoval", new Dictionary<string, object> { ["CertificateName"] = Name });
    }
}

[Cmdlet(VerbsCommon.Remove, "AzKeyVaultCertificate", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType("Microsoft.Azure.Azclips.Extensions.KeyVault.Models.DeletedCertificateBundle")]
public class RemoveAzKeyVaultCertificateCmdlet : KeyVaultCmdletBase
{
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string Name { get; set; } = string.Empty;

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"{VaultName ?? VaultUrl}/{Name}", "Delete all certificate versions (does not purge)")) return;
        Execute("Remove-AzKeyVaultCertificate", new Dictionary<string, object> { ["CertificateName"] = Name, ["Yes"] = true });
    }
}