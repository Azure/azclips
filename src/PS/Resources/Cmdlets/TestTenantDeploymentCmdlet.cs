// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsDiagnostic.Test, "AzTenantDeployment")]
public class TestTenantDeploymentCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Alias("DeploymentName")]
    [Parameter(Mandatory = false)]
    public string? Name { get; set; }

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = string.Empty;

    [Parameter(Mandatory = false)] public string? TemplateFile { get; set; }
    [Parameter(Mandatory = false)] public string? TemplateUri { get; set; }
    [Parameter(Mandatory = false)] public string? TemplateSpecId { get; set; }
    [Parameter(Mandatory = false)] public Hashtable? TemplateObject { get; set; }
    [Parameter(Mandatory = false)] public string? QueryString { get; set; }
    [Parameter(Mandatory = false)] public string? TemplateParameterFile { get; set; }
    [Parameter(Mandatory = false)] public string? TemplateParameterUri { get; set; }
    [Parameter(Mandatory = false)] public Hashtable? TemplateParameterObject { get; set; }
    [Parameter(Mandatory = false)] public string[]? Parameter { get; set; }
    [Parameter(Mandatory = false)] public string? ValidationLevel { get; set; }
    [Parameter(Mandatory = false)] public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "TenantDeploymentValidationError", ErrorCategory.InvalidData);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        Dictionary<string, object> parameters = new() { [nameof(Location)] = Location };
        foreach (string name in new[]
        {
            nameof(Name), nameof(TemplateFile), nameof(TemplateUri), nameof(TemplateSpecId),
            nameof(QueryString), nameof(Parameter), nameof(ValidationLevel), nameof(Profile),
        })
        {
            if (MyInvocation.BoundParameters.TryGetValue(name, out object? value) && value is not null)
            {
                parameters[name] = value;
            }
        }
        if (TemplateObject is not null)
        {
            parameters[nameof(TemplateObject)] = TenantDeploymentCmdletHelpers.NormalizeJsonInput(TemplateObject)!;
        }
        if (TemplateParameterObject is not null)
        {
            parameters["ParametersObject"] =
                TenantDeploymentCmdletHelpers.NormalizeJsonInput(TemplateParameterObject)!;
        }
        if (TemplateParameterFile is not null)
        {
            parameters["ParametersFile"] = TemplateParameterFile;
        }
        if (TemplateParameterUri is not null)
        {
            parameters["ParametersUri"] = TemplateParameterUri;
        }
        TenantDeploymentCmdletHelpers.Run(this, engine, streams, _cts.Token, "Test-AzTenantDeployment", parameters);
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}

internal static class TenantDeploymentCmdletHelpers
{
    public static object? NormalizeJsonInput(object? value) =>
        value switch
        {
            IDictionary dictionary => dictionary.Cast<DictionaryEntry>().ToDictionary(
                entry => entry.Key.ToString()!,
                entry => NormalizeJsonInput(entry.Value)),
            IEnumerable values when value is not string =>
                values.Cast<object?>().Select(NormalizeJsonInput).ToArray(),
            _ => value,
        };

    public static string NameFromId(string id)
    {
        string[] segments = id.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 4
            || !string.Equals(segments[0], "providers", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[1], "Microsoft.Resources", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[2], "deployments", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Tenant deployment Id must be /providers/Microsoft.Resources/deployments/{name}.",
                nameof(id));
        }
        return segments[3];
    }

    public static void Run(
        PSCmdlet cmdlet,
        IEngine engine,
        OutputStreams streams,
        CancellationToken token,
        string command,
        Dictionary<string, object> parameters)
    {
        ITelemetryService telemetry = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetry.StartRootActivity();
        CommandInvocation invocation = new(
            "Resources",
            command,
            parameters,
            false,
            cmdlet.MyInvocation.MyCommand.ModuleName,
            cmdlet.MyInvocation.MyCommand.Version?.ToString());
        telemetry.TagInvocation(invocation);
        using ISession session = engine.OpenExecutionSession(invocation, streams, token);
        session.Run();
    }

    // The job owns its own cancellation token (only cancelled via Stop-Job), independent from the
    // invoking cmdlet's lifecycle, since the cmdlet call returns immediately after this.
    public static EngineCommandJob RunAsJob(
        PSCmdlet cmdlet,
        IEngine engine,
        string command,
        Dictionary<string, object> parameters,
        Action<EngineCommandJob>? onSuccess = null)
    {
        var job = new EngineCommandJob(command, cmdlet.MyInvocation.MyCommand.Name);
        cmdlet.JobRepository.Add(job);

        ITelemetryService telemetry = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        CommandInvocation invocation = new(
            "Resources",
            command,
            parameters,
            false,
            cmdlet.MyInvocation.MyCommand.ModuleName,
            cmdlet.MyInvocation.MyCommand.Version?.ToString());

        job.Start(async () =>
        {
            using var activity = telemetry.StartRootActivity();
            telemetry.TagInvocation(invocation);
            OutputStreams streams = JobOutputStreamsFactory.Create(job, $"{command}Error");
            using ISession session = engine.OpenExecutionSession(invocation, streams, job.CancellationToken);
            await session.RunAsync();
            onSuccess?.Invoke(job);
        });

        return job;
    }
}
