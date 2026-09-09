using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[Cmdlet(VerbsCommon.New, "AzStorageAccount", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Storage.Models.StorageAccount")]
public class NewStorageAccountCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string SkuName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Kind { get; set; } = "";

    [Parameter(Mandatory = false)]
    public Hashtable? Tag { get; set; }

    [Parameter(Mandatory = false)]
    public string? AccessTier { get; set; }

    [Parameter(Mandatory = false)]
    public bool EnableHttpsTrafficOnly { get; set; }

    [Parameter(Mandatory = false)]
    public bool AllowBlobPublicAccess { get; set; }

    [Parameter(Mandatory = false)]
    public string? MinimumTlsVersion { get; set; }

    [Parameter(Mandatory = false)]
    public string? PublicNetworkAccess { get; set; }

    [Parameter(Mandatory = false)]
    public bool EnableHierarchicalNamespace { get; set; }

    [Parameter(Mandatory = false)]
    public bool EnableSftp { get; set; }

    [Parameter(Mandatory = false)]
    public bool EnableLocalUser { get; set; }

    [Parameter(Mandatory = false)]
    public bool EnableNfsV3 { get; set; }

    [Parameter(Mandatory = false)]
    public string? DnsEndpointType { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? Zone { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "StorageAccountError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"Storage account '{Name}' in resource group '{ResourceGroupName}'", "Create"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>
        {
            { nameof(ResourceGroupName), ResourceGroupName },
            { nameof(Name), Name },
            { nameof(Location), Location },
            { nameof(SkuName), SkuName },
            { nameof(Kind), Kind },
        };

        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId))) { parameters.Add(nameof(SubscriptionId), SubscriptionId); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Tag))) { parameters.Add(nameof(Tag), ConvertTags(Tag!)); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AccessTier))) { parameters.Add(nameof(AccessTier), AccessTier!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableHttpsTrafficOnly))) { parameters.Add(nameof(EnableHttpsTrafficOnly), EnableHttpsTrafficOnly); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AllowBlobPublicAccess))) { parameters.Add(nameof(AllowBlobPublicAccess), AllowBlobPublicAccess); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(MinimumTlsVersion))) { parameters.Add(nameof(MinimumTlsVersion), MinimumTlsVersion!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PublicNetworkAccess))) { parameters.Add(nameof(PublicNetworkAccess), PublicNetworkAccess!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableHierarchicalNamespace))) { parameters.Add(nameof(EnableHierarchicalNamespace), EnableHierarchicalNamespace); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableSftp))) { parameters.Add(nameof(EnableSftp), EnableSftp); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableLocalUser))) { parameters.Add(nameof(EnableLocalUser), EnableLocalUser); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableNfsV3))) { parameters.Add(nameof(EnableNfsV3), EnableNfsV3); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(DnsEndpointType))) { parameters.Add(nameof(DnsEndpointType), DnsEndpointType!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Zone))) { parameters.Add(nameof(Zone), Zone!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile))) { parameters.Add(nameof(Profile), Profile!); }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Storage", "New-AzStorageAccount", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();

    private static Dictionary<string, string> ConvertTags(Hashtable tags)
    {
        var converted = new Dictionary<string, string>();
        foreach (DictionaryEntry entry in tags)
        {
            if (entry.Key is not null)
            {
                converted[entry.Key.ToString()!] = entry.Value?.ToString() ?? string.Empty;
            }
        }

        return converted;
    }
}