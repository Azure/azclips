// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Text.Json;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;
using Core.Telemetry;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsLifecycle.Invoke, "AzRestMethod")]
[OutputType(typeof(PSObject), typeof(object[]), typeof(string), typeof(long), typeof(decimal), typeof(double), typeof(bool),
    typeof(Microsoft.Azure.Azclips.Extensions.Resources.Models.RestResponse))]
public class InvokeAzclipsRestMethodCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    [ValidateSet("GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", IgnoreCase = true)]
    public string Method { get; set; } = "GET";

    [Parameter(Mandatory = false)]
    public string Uri { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? Payload { get; set; }

    /// <summary>
    /// Request headers, either as a hashtable (<c>@{ 'Accept' = 'application/json' }</c>) or as a
    /// string array in KEY=VALUE form (<c>'Accept=application/json'</c>).
    /// </summary>
    [Parameter(Mandatory = false)]
    public object? Headers { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter FullResponse { get; set; }

    // ── ByParameters mode (piece-wise ARM URL builder) ──

    /// <summary>Subscription id used to build an ARM URL from piece-wise parameters. Mutually exclusive with Uri.</summary>
    [Parameter(Mandatory = false)]
    public string? SubscriptionId { get; set; }

    /// <summary>Resource group name segment for a piece-wise ARM URL. Mutually exclusive with Uri.</summary>
    [Parameter(Mandatory = false)]
    public string? ResourceGroupName { get; set; }

    /// <summary>Resource provider namespace (e.g. Microsoft.Resources) for a piece-wise ARM URL. Mutually exclusive with Uri.</summary>
    [Parameter(Mandatory = false)]
    public string? ResourceProviderName { get; set; }

    /// <summary>Resource type segments, interleaved with Name. Mutually exclusive with Uri.</summary>
    [Parameter(Mandatory = false)]
    public string[]? ResourceType { get; set; }

    /// <summary>Resource name segments, interleaved with ResourceType. Mutually exclusive with Uri.</summary>
    [Parameter(Mandatory = false)]
    public string[]? Name { get; set; }

    /// <summary>api-version for a piece-wise ARM URL. Required in ByParameters mode. Mutually exclusive with Uri.</summary>
    [Parameter(Mandatory = false)]
    public string? ApiVersion { get; set; }

    /// <summary>
    /// Extra query-string parameters, either as a hashtable (<c>@{ '$top' = '3' }</c>) or as a string
    /// array in KEY=VALUE form (<c>'$top=3'</c>). Appended to the final URL in either mode.
    /// </summary>
    [Parameter(Mandatory = false)]
    public object? UriParameters { get; set; }

    /// <summary>Use the named profile for this command, overriding the active profile.</summary>
    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "ResourcesError", ErrorCategory.NotSpecified);
        var writeOutput = streams.WriteOutput;
        streams = streams with
        {
            WriteOutput = (value, ct) =>
            {
                if (value is JsonElement json)
                {
                    WriteObject(ConvertJson(json), enumerateCollection: true);
                    return Task.CompletedTask;
                }

                return writeOutput(value, ct);
            }
        };
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    private static object? ConvertJson(JsonElement json)
    {
        switch (json.ValueKind)
        {
            case JsonValueKind.Object:
                var result = new PSObject();
                foreach (var property in json.EnumerateObject())
                    result.Properties.Add(new PSNoteProperty(property.Name, ConvertJson(property.Value)));
                return result;
            case JsonValueKind.Array:
                return json.EnumerateArray().Select(ConvertJson).ToArray();
            case JsonValueKind.String:
                return json.GetString();
            case JsonValueKind.Number:
                if (json.TryGetInt64(out long integer))
                    return integer;
                if (json.TryGetDecimal(out decimal number))
                    return number;
                return json.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
                return null;
            default:
                throw new ArgumentException($"Unsupported JSON value kind: {json.ValueKind}", nameof(json));
        }
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>
        {
            { "Method", Method },
            { "FullResponse", FullResponse.IsPresent },
        };
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Uri)))
        {
            parameters.Add("Uri", Uri);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Payload)))
        {
            parameters.Add("Payload", Payload!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Headers)) && Headers is not null)
        {
            parameters.Add("Headers", RestProcessor_Orchestrator.NormalizeKeyValue(Headers));
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
        {
            parameters.Add("SubscriptionId", SubscriptionId!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ResourceGroupName)))
        {
            parameters.Add("ResourceGroupName", ResourceGroupName!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ResourceProviderName)))
        {
            parameters.Add("ResourceProviderName", ResourceProviderName!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ResourceType)) && ResourceType is not null)
        {
            parameters.Add("ResourceType", ResourceType);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Name)) && Name is not null)
        {
            parameters.Add("Name", Name);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ApiVersion)))
        {
            parameters.Add("ApiVersion", ApiVersion!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(UriParameters)) && UriParameters is not null)
        {
            parameters.Add("UriParameters", RestProcessor_Orchestrator.NormalizeKeyValue(UriParameters));
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Resources", "Invoke-AzRestMethod", parameters, false, moduleName, moduleVersion);
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
