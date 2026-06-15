using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Core.Telemetry;

namespace Microsoft.Azure.Cirrus.Cmdlets.Rest;

[Cmdlet(VerbsLifecycle.Invoke, "CirrusRestMethod")]
public class InvokeCirrusRestMethodCmdlet : PSCmdlet
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
                    "RestError",
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
        var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>
        {
            { "Method", Method },
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
            parameters.Add("Headers", NormalizeKeyValue(Headers));
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
            parameters.Add("UriParameters", NormalizeKeyValue(UriParameters));
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Rest", "Invoke-CirrusRestMethod", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    /// <summary>
    /// Normalizes a KEY=VALUE argument (headers or uri-parameters) to a <c>string[]</c> of
    /// <c>KEY=VALUE</c> entries, accepting either a hashtable/dictionary or an existing string collection.
    /// </summary>
    private static string[] NormalizeKeyValue(object value)
    {
        if (value is IDictionary dictionary)
        {
            var entries = new List<string>();
            foreach (DictionaryEntry entry in dictionary)
            {
                entries.Add($"{entry.Key}={entry.Value}");
            }
            return entries.ToArray();
        }

        if (value is string single)
        {
            return [single];
        }

        if (value is IEnumerable enumerable)
        {
            var entries = new List<string>();
            foreach (var item in enumerable)
            {
                entries.Add(item?.ToString() ?? string.Empty);
            }
            return entries.ToArray();
        }

        return [value.ToString() ?? string.Empty];
    }

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();
}
