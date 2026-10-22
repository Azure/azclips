// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.Set, "AzResourceGroup", DefaultParameterSetName = ResourceGroupNameParameterSet)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Models.ResourceGroup")]
public class SetResourceGroupCmdlet : PSCmdlet
{
    private const string ResourceGroupNameParameterSet = "SetByResourceGroupName";
    private const string ResourceGroupIdParameterSet = "SetByResourceGroupId";

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Alias("ResourceGroupName")]
    [Parameter(Mandatory = true, ParameterSetName = ResourceGroupNameParameterSet, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string Name { get; set; } = string.Empty;

    [Alias("ResourceGroupId", "ResourceId")]
    [Parameter(Mandatory = true, ParameterSetName = ResourceGroupIdParameterSet)]
    [ValidateNotNullOrEmpty]
    public string Id { get; set; } = string.Empty;

    [Alias("Tags")]
    [Parameter(Mandatory = true, Position = 1, ValueFromPipelineByPropertyName = true)]
    public Hashtable Tag { get; set; } = new();

    [Parameter(Mandatory = false)]
    public string? SubscriptionId { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "ResourceGroupError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var parameters = new Dictionary<string, object>
        {
            [nameof(Tag)] = ToStringDictionary(Tag),
        };

        if (ParameterSetName == ResourceGroupIdParameterSet)
        {
            parameters[nameof(Id)] = Id;
        }
        else
        {
            parameters[nameof(Name)] = Name;
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
        {
            parameters[nameof(SubscriptionId)] = SubscriptionId!;
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters[nameof(Profile)] = Profile!;
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation(
            "Resources",
            "Set-AzResourceGroup",
            parameters,
            false,
            moduleName,
            moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();

    private static Dictionary<string, string> ToStringDictionary(Hashtable tags)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in tags)
        {
            string key = entry.Key?.ToString()
                ?? throw AzclipsErrorException.InvalidInput("Tag keys cannot be null.");
            if (string.IsNullOrWhiteSpace(key))
            {
                throw AzclipsErrorException.InvalidInput("Tag keys cannot be empty.");
            }

            result[key] = entry.Value?.ToString() ?? string.Empty;
        }

        return result;
    }
}
