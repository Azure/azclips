// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Sql;

[Cmdlet(VerbsCommon.New, "AzSqlServer", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Sql.Models.SqlServer")]
public class NewSqlServerCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string AdministratorLogin { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string AdministratorLoginPassword { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? ServerVersion { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("None", "1.0", "1.1", "1.2", "1.3")]
    public string? MinimalTlsVersion { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("Enabled", "Disabled", "SecuredByPerimeter")]
    public string? PublicNetworkAccess { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("Tag")]
    public Hashtable? Tags { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "SqlServerError", ErrorCategory.NotSpecified);
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
            { "Name", Name },
            { "Location", Location },
            { "AdministratorLogin", AdministratorLogin },
            { "AdministratorLoginPassword", AdministratorLoginPassword },
        };
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ServerVersion)))       { parameters.Add("ServerVersion", ServerVersion!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(MinimalTlsVersion)))   { parameters.Add("MinimalTlsVersion", MinimalTlsVersion!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PublicNetworkAccess))) { parameters.Add("PublicNetworkAccess", PublicNetworkAccess!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Tags)))                { parameters.Add("Tags", ToTagArray(Tags!)); }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Sql", "New-AzSqlServer", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    // ponytail: the engine binds by exact CLR type, so the hashtable is flattened to key=value
    // strings here rather than teaching the shared binder about dictionaries.
    private static string[] ToTagArray(Hashtable tags) =>
        tags.Cast<DictionaryEntry>()
            .Select(entry => $"{entry.Key}={entry.Value}")
            .ToArray();

    protected override void StopProcessing() => _cts.Cancel();
}
