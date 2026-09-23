// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsDiagnostic.Test, "AzTenantDeployment")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentValidateResult")]
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
            nameof(TemplateObject), nameof(QueryString), nameof(TemplateParameterFile),
            nameof(TemplateParameterUri), nameof(TemplateParameterObject), nameof(Parameter),
            nameof(ValidationLevel), nameof(Profile),
        })
        {
            if (MyInvocation.BoundParameters.TryGetValue(name, out object? value) && value is not null)
            {
                parameters[name] = value;
            }
        }
        TenantDeploymentCmdletHelpers.Run(this, engine, streams, _cts.Token, "Test-AzTenantDeployment", parameters);
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}

internal static class TenantDeploymentCmdletHelpers
{
    public static string NameFromId(string id)
    {
        string[] segments = id.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2
            || !string.Equals(segments[^2], "deployments", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Tenant deployment Id must end with /providers/Microsoft.Resources/deployments/{name}.", nameof(id));
        }
        return segments[^1];
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
}
