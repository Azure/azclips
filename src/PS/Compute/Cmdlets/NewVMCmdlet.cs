using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Core.Telemetry;

namespace Microsoft.Azure.Cirrus.Cmdlets.Compute;

[Cmdlet(VerbsCommon.New, "VM", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public class NewVMCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string VmSize { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string AdminUsername { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string AdminPassword { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string NetworkInterfaceId { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? OsDiskCreateOption { get; set; }

    [Parameter(Mandatory = false)]
    public string? ImagePublisher { get; set; }

    [Parameter(Mandatory = false)]
    public string? ImageOffer { get; set; }

    [Parameter(Mandatory = false)]
    public string? ImageSku { get; set; }

    [Parameter(Mandatory = false)]
    public string? ImageVersion { get; set; }

    [Parameter(Mandatory = false)]
    public string? SecurityType { get; set; }

    [Parameter(Mandatory = false)]
    public bool? SecureBootEnabled { get; set; }

    [Parameter(Mandatory = false)]
    public bool? VTpmEnabled { get; set; }

    [Parameter(Mandatory = false)]
    public bool? BootDiagnosticsEnabled { get; set; }

    protected override void BeginProcessing()
    {
        streams = new OutputStreams(
            WriteOutput: (value, ct) => { WriteObject(value); return Task.CompletedTask; },
            WriteVerbose: (value, ct) => { WriteVerbose(value?.ToString() ?? ""); return Task.CompletedTask; },
            WriteDebug: (value, ct) => { WriteDebug(value?.ToString() ?? ""); return Task.CompletedTask; },
            WriteWarning: (value, ct) => { WriteWarning(value?.ToString() ?? ""); return Task.CompletedTask; },
            WriteError: (value, ct) =>
            {
                WriteError(new ErrorRecord(
                    new Exception(value?.ToString() ?? ""),
                    "VMError",
                    ErrorCategory.NotSpecified,
                    null));
                return Task.CompletedTask;
            },
            WriteInformation: (value, ct) =>
            {
                WriteInformation(new InformationRecord(value, null));
                return Task.CompletedTask;
            });
        engine = CirrusServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        using var activity = CirrusServiceProvider.GetRequiredService<ITelemetryService>().StartPowerShellActivity(MyInvocation.MyCommand.Name, MyInvocation.BoundParameters);
        var parameters = new Dictionary<string, object>
        {
            { "SubscriptionId", SubscriptionId },
            { "Name", Name },
            { "Location", Location },
            { "ResourceGroupName", ResourceGroupName },
            { "VmSize", VmSize },
            { "AdminUsername", AdminUsername },
            { "AdminPassword", AdminPassword },
            { "NetworkInterfaceId", NetworkInterfaceId },
        };
        if (MyInvocation.BoundParameters.ContainsKey(nameof(OsDiskCreateOption)))     { parameters.Add("OsDiskCreateOption", OsDiskCreateOption!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ImagePublisher)))         { parameters.Add("ImagePublisher", ImagePublisher!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ImageOffer)))             { parameters.Add("ImageOffer", ImageOffer!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ImageSku)))               { parameters.Add("ImageSku", ImageSku!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ImageVersion)))           { parameters.Add("ImageVersion", ImageVersion!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SecurityType)))           { parameters.Add("SecurityType", SecurityType!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SecureBootEnabled)))      { parameters.Add("SecureBootEnabled", SecureBootEnabled!.Value); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(VTpmEnabled)))            { parameters.Add("VTpmEnabled", VTpmEnabled!.Value); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(BootDiagnosticsEnabled))) { parameters.Add("BootDiagnosticsEnabled", BootDiagnosticsEnabled!.Value); }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Compute", "New-VM", parameters, false, moduleName, moduleVersion);

        using IInvocationSession session = engine.InvokeSession(invocation, streams, _cts.Token);
        session.Process();
    }

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();
}
