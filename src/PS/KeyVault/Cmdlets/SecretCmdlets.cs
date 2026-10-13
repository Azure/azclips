using System.Collections;
using System.Globalization;
using System.Management.Automation;
using System.Security;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.KeyVault;

public abstract class KeyVaultCmdletBase : PSCmdlet
{
    private readonly CancellationTokenSource cancellation = new();
    private IEngine engine = null!;
    private ITelemetryService telemetry = null!;
    private OutputStreams streams = null!;

    [Parameter]
    public string? VaultName { get; set; }

    [Parameter]
    public string? VaultUrl { get; set; }

    [Parameter]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
        telemetry = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        streams = CmdletOutputStreamsFactory.Create(this, "KeyVaultError", ErrorCategory.NotSpecified, enumerateOutput: true);
    }

    protected void Execute(string command, Dictionary<string, object> parameters)
    {
        if (VaultName is not null) parameters.Add(nameof(VaultName), VaultName);
        if (VaultUrl is not null) parameters.Add(nameof(VaultUrl), VaultUrl);
        if (Profile is not null) parameters.Add(nameof(Profile), Profile);
        using var activity = telemetry.StartRootActivity();
        var invocation = new CommandInvocation("KeyVault", command, parameters, false,
            MyInvocation.MyCommand.ModuleName, MyInvocation.MyCommand.Version?.ToString());
        telemetry.TagInvocation(invocation);
        using var session = engine.OpenExecutionSession(invocation, streams, cancellation.Token);
        try { session.Run(); }
        finally { parameters.Remove("SecureValue"); }
    }

    protected override void EndProcessing() => cancellation.Dispose();
    protected override void StopProcessing() => cancellation.Cancel();
}

public abstract class SecretMutationCmdletBase : KeyVaultCmdletBase
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
    public string? ContentType { get; set; }

    [Parameter]
    [Alias("Tags")]
    public Hashtable? Tag { get; set; }

    protected Dictionary<string, object> MutationParameters()
    {
        var parameters = new Dictionary<string, object> { ["SecretName"] = Name };
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Enabled))) parameters.Add("Enabled", Enabled.ToString());
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Expires))) parameters.Add("Expires", Expires.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        if (MyInvocation.BoundParameters.ContainsKey(nameof(NotBefore))) parameters.Add("NotBefore", NotBefore.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        if (ContentType is not null) parameters.Add("ContentType", ContentType);
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

[Cmdlet(VerbsCommon.Set, "AzKeyVaultSecret", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.KeyVault.Customization.SecretMetadata")]
public class SetAzKeyVaultSecretCmdlet : SecretMutationCmdletBase
{
    [Parameter(Mandatory = true)]
    [ValidateNotNull]
    public SecureString SecretValue { get; set; } = null!;

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"{VaultName ?? VaultUrl}/{Name}", "Set secret (creates a new version)")) return;
        var parameters = MutationParameters();
        parameters.Add("SecureValue", SecretValue);
        Execute("Set-AzKeyVaultSecret", parameters);
    }
}

[Cmdlet(VerbsCommon.Get, "AzKeyVaultSecret", DefaultParameterSetName = "List")]
[OutputType("Microsoft.Azure.Azclips.Extensions.KeyVault.Customization.SecretMetadata", "System.String")]
public class GetAzKeyVaultSecretCmdlet : KeyVaultCmdletBase
{
    [Parameter(Mandatory = true, ParameterSetName = "Get")]
    [Parameter(Mandatory = true, ParameterSetName = "Versions")]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    [Parameter(ParameterSetName = "Get")]
    public string? Version { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Versions")]
    public SwitchParameter IncludeVersions { get; set; }

    [Parameter(ParameterSetName = "Get")]
    public SwitchParameter AsPlainText { get; set; }

    [Parameter(ParameterSetName = "List")]
    [Parameter(ParameterSetName = "Versions")]
    [ValidateRange(1, 25)]
    public int MaxResults { get; set; } = 25;

    protected override void ProcessRecord()
    {
        var parameters = new Dictionary<string, object>();
        if (Name is not null) parameters.Add("SecretName", Name);
        if (Version is not null) parameters.Add("SecretVersion", Version);
        if (ParameterSetName == "Get") parameters.Add("AsPlainText", AsPlainText.IsPresent);
        else parameters.Add("PageSize", MaxResults);
        Execute($"Get-AzKeyVaultSecret_{ParameterSetName}", parameters);
    }
}

[Cmdlet(VerbsData.Update, "AzKeyVaultSecret", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.KeyVault.Customization.SecretMetadata")]
public class UpdateAzKeyVaultSecretCmdlet : SecretMutationCmdletBase
{
    [Parameter]
    public string? Version { get; set; }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"{VaultName ?? VaultUrl}/{Name}", "Update secret attributes")) return;
        var parameters = MutationParameters();
        if (Version is not null) parameters.Add("SecretVersion", Version);
        Execute("Update-AzKeyVaultSecret", parameters);
    }
}

[Cmdlet(VerbsCommon.Undo, "AzKeyVaultSecretRemoval", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.KeyVault.Customization.SecretMetadata")]
public class UndoAzKeyVaultSecretRemovalCmdlet : KeyVaultCmdletBase
{
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string Name { get; set; } = string.Empty;

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"{VaultName ?? VaultUrl}/{Name}", "Recover a soft-deleted secret and its versions")) return;
        Execute("Undo-AzKeyVaultSecretRemoval", new Dictionary<string, object> { ["SecretName"] = Name });
    }
}

[Cmdlet(VerbsCommon.Remove, "AzKeyVaultSecret", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType("Microsoft.Azure.Azclips.Extensions.KeyVault.Customization.SecretMetadata")]
public class RemoveAzKeyVaultSecretCmdlet : KeyVaultCmdletBase
{
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string Name { get; set; } = string.Empty;

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"{VaultName ?? VaultUrl}/{Name}", "Delete all secret versions (does not purge)")) return;
        Execute("Remove-AzKeyVaultSecret", new Dictionary<string, object> { ["SecretName"] = Name, ["Yes"] = true });
    }
}