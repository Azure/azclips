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

    [Parameter(Mandatory = true)]
    public string Uri { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? Payload { get; set; }

    /// <summary>
    /// Request headers, either as a hashtable (<c>@{ 'Accept' = 'application/json' }</c>) or as a
    /// string array in KEY=VALUE form (<c>'Accept=application/json'</c>).
    /// </summary>
    [Parameter(Mandatory = false)]
    public object? Headers { get; set; }

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
        using var activity = CirrusServiceProvider.GetRequiredService<ITelemetryService>().StartPowerShellActivity(MyInvocation.MyCommand.Name, MyInvocation.BoundParameters);
        var parameters = new Dictionary<string, object>
        {
            { "Method", Method },
            { "Uri", Uri },
        };
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Payload)))
        {
            parameters.Add("Payload", Payload!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Headers)) && Headers is not null)
        {
            parameters.Add("Headers", NormalizeHeaders(Headers));
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Rest", "Invoke-CirrusRestMethod", parameters, false, moduleName, moduleVersion);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    /// <summary>
    /// Normalizes the Headers argument to a <c>string[]</c> of <c>KEY=VALUE</c> entries, accepting
    /// either a hashtable/dictionary or an existing string collection.
    /// </summary>
    private static string[] NormalizeHeaders(object headers)
    {
        if (headers is IDictionary dictionary)
        {
            var entries = new List<string>();
            foreach (DictionaryEntry entry in dictionary)
            {
                entries.Add($"{entry.Key}={entry.Value}");
            }
            return entries.ToArray();
        }

        if (headers is string single)
        {
            return [single];
        }

        if (headers is IEnumerable enumerable)
        {
            var entries = new List<string>();
            foreach (var item in enumerable)
            {
                entries.Add(item?.ToString() ?? string.Empty);
            }
            return entries.ToArray();
        }

        return [headers.ToString() ?? string.Empty];
    }

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();
}
