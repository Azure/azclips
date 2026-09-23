// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsLifecycle.Stop, "AzTenantDeployment", DefaultParameterSetName = NameSet, SupportsShouldProcess = true)]
[OutputType(typeof(bool))]
public class StopTenantDeploymentCmdlet : PSCmdlet
{
    private const string NameSet = "StopByName";
    private const string IdSet = "StopById";
    private const string ObjectSet = "StopByInputObject";
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Alias("DeploymentName")]
    [Parameter(Position = 0, Mandatory = true, ParameterSetName = NameSet)]
    public string? Name { get; set; }

    [Alias("DeploymentId", "ResourceId")]
    [Parameter(Mandatory = true, ParameterSetName = IdSet)]
    public string? Id { get; set; }

    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ObjectSet)]
    public DeploymentExtended? InputObject { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter PassThru { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "TenantDeploymentError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        string name = Name ?? (Id is not null ? TenantDeploymentCmdletHelpers.NameFromId(Id) : InputObject?.Name)
            ?? throw new ArgumentException("A tenant deployment name is required.");
        if (!ShouldProcess($"tenant deployment '{name}'", "Cancel"))
        {
            return;
        }

        Dictionary<string, object> parameters = new() { ["Name"] = name };
        if (Profile is not null)
        {
            parameters[nameof(Profile)] = Profile;
        }
        TenantDeploymentCmdletHelpers.Run(this, engine, streams, _cts.Token, "Stop-AzTenantDeployment", parameters);
        if (PassThru)
        {
            WriteObject(true);
        }
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
