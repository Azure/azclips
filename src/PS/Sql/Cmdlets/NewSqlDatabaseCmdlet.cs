// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Sql;

[Cmdlet(VerbsCommon.New, "AzSqlDatabase", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Sql.Models.SqlDatabase")]
public class NewSqlDatabaseCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string ServerName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string DatabaseName { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? Edition { get; set; }

    [Parameter(Mandatory = false)]
    public string? RequestedServiceObjectiveName { get; set; }

    [Parameter(Mandatory = false)]
    public string? Family { get; set; }

    [Parameter(Mandatory = false)]
    public int? Capacity { get; set; }

    [Parameter(Mandatory = false)]
    public string? CollationName { get; set; }

    [Parameter(Mandatory = false)]
    public long? MaxSizeBytes { get; set; }

    [Parameter(Mandatory = false)]
    public string? ElasticPoolName { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter ZoneRedundant { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "SqlDatabaseError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>
        {
            { "SubscriptionId", SubscriptionId },
            { "ResourceGroupName", ResourceGroupName },
            { "ServerName", ServerName },
            { "DatabaseName", DatabaseName },
        };
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Edition)))                       { parameters.Add("Edition", Edition!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RequestedServiceObjectiveName))) { parameters.Add("RequestedServiceObjectiveName", RequestedServiceObjectiveName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Family)))                        { parameters.Add("Family", Family!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Capacity)))                      { parameters.Add("Capacity", Capacity!.Value); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(CollationName)))                 { parameters.Add("CollationName", CollationName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(MaxSizeBytes)))                  { parameters.Add("MaxSizeBytes", MaxSizeBytes!.Value); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ElasticPoolName)))               { parameters.Add("ElasticPoolName", ElasticPoolName!); }
        // ponytail: a SwitchParameter is not IConvertible, and the binder runs everything through
        // Convert.ChangeType — hand the engine the plain bool.
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ZoneRedundant)))                 { parameters.Add("ZoneRedundant", ZoneRedundant.IsPresent); }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Sql", "New-AzSqlDatabase", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();
}
