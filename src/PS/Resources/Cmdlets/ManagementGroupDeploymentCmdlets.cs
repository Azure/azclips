// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Management.Automation.Runspaces;
using System.Net;
using System.Security;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

internal static class ManagementGroupDeploymentCmdletHelpers
{
    // Legacy PS ResourceManager.format.ps1xml PSDeployment list view (the String properties hold the formatted tables).
    private static readonly string[] s_deploymentView =
    [
        "Id", "DeploymentName", "ManagementGroupId", "Location", "ProvisioningState", "Timestamp", "Mode",
        "TemplateLinkString", "ParametersString", "OutputsString", "DeploymentDebugLogLevel",
    ];

    // Same view as format data, so the labels read TemplateLink/Parameters/Outputs like legacy. The module packaging
    // ships no ps1xml, so it is loaded at run time; s_deploymentView stays as the fallback.
    private const string DeploymentFormat = """
        <?xml version="1.0" encoding="utf-8"?>
        <Configuration><ViewDefinitions><View>
          <Name>Microsoft.Azure.Azclips.Extensions.Resources.Processors.PSDeployment</Name>
          <ViewSelectedBy><TypeName>Microsoft.Azure.Azclips.Extensions.Resources.Processors.PSDeployment</TypeName></ViewSelectedBy>
          <ListControl><ListEntries><ListEntry><ListItems>
            <ListItem><Label>Id</Label><PropertyName>Id</PropertyName></ListItem>
            <ListItem><Label>DeploymentName</Label><PropertyName>DeploymentName</PropertyName></ListItem>
            <ListItem><ItemSelectionCondition><ScriptBlock>$_.ManagementGroupId -ne $null</ScriptBlock></ItemSelectionCondition><Label>ManagementGroupId</Label><PropertyName>ManagementGroupId</PropertyName></ListItem>
            <ListItem><ItemSelectionCondition><ScriptBlock>$_.Location -ne $null</ScriptBlock></ItemSelectionCondition><Label>Location</Label><PropertyName>Location</PropertyName></ListItem>
            <ListItem><Label>ProvisioningState</Label><PropertyName>ProvisioningState</PropertyName></ListItem>
            <ListItem><Label>Timestamp</Label><PropertyName>Timestamp</PropertyName></ListItem>
            <ListItem><Label>Mode</Label><PropertyName>Mode</PropertyName></ListItem>
            <ListItem><Label>TemplateLink</Label><PropertyName>TemplateLinkString</PropertyName></ListItem>
            <ListItem><Label>Parameters</Label><PropertyName>ParametersString</PropertyName></ListItem>
            <ListItem><Label>Outputs</Label><PropertyName>OutputsString</PropertyName></ListItem>
            <ListItem><Label>DeploymentDebugLogLevel</Label><PropertyName>DeploymentDebugLogLevel</PropertyName></ListItem>
          </ListItems></ListEntry></ListEntries></ListControl>
        </View></ViewDefinitions></Configuration>
        """;

    private static readonly HashSet<Guid> s_formatRunspaces = [];

    private static void EnsureDeploymentFormat(PSCmdlet cmdlet)
    {
        Guid runspace = Runspace.DefaultRunspace?.InstanceId ?? Guid.Empty;
        lock (s_formatRunspaces)
        {
            // Background (job) threads have no default runspace; the caller thread loaded the format already.
            if (runspace == Guid.Empty || s_formatRunspaces.Contains(runspace))
            {
                return;
            }
        }
        try
        {
            string path = Path.Combine(Path.GetTempPath(), "Azclips.Resources.PSDeployment.format.ps1xml");
            if (!File.Exists(path) || File.ReadAllText(path) != DeploymentFormat)
            {
                File.WriteAllText(path, DeploymentFormat);
            }
            cmdlet.InvokeCommand.InvokeScript(false, ScriptBlock.Create("Update-FormatData -PrependPath $args[0]"), null, path);
            lock (s_formatRunspaces)
            {
                s_formatRunspaces.Add(runspace);
            }
        }
        catch (Exception)
        {
            // Not on the pipeline thread, or format files blocked by policy: DefaultDisplayPropertySet is used instead.
        }
    }

    // Legacy PS GetTemplateContentFromTemplateSpec: properties.mainTemplate of the template spec version.
    private static JsonNode? ReadTemplateSpec(PSCmdlet cmdlet, string templateSpecId)
    {
        RestResponse? response = null;
        OutputStreamDelegate ignore = (_, _) => Task.CompletedTask;
        OutputStreams quiet = new(
            (value, _) => { response = value as RestResponse; return Task.CompletedTask; },
            ignore, ignore, ignore, ignore, ignore);
        Run(cmdlet, AzclipsServiceProvider.GetRequiredService<IEngine>(), quiet, CancellationToken.None, "Invoke-AzRestMethod",
            new() { ["Method"] = "GET", ["Uri"] = $"{templateSpecId.Trim('"')}?api-version=2022-02-01" });
        return response is { StatusCode: >= 200 and < 300 }
            ? JsonNode.Parse(response.Content)?["properties"]?["mainTemplate"]
            : null;
    }

    public static object? NormalizeJsonInput(object? value) =>
        value switch
        {
            PSObject psObject => NormalizeJsonInput(psObject.BaseObject),
            SecureString secure => new NetworkCredential(string.Empty, secure).Password,
            IDictionary dictionary => dictionary.Cast<DictionaryEntry>().ToDictionary(
                entry => entry.Key.ToString()!,
                entry => NormalizeJsonInput(entry.Value)),
            IEnumerable values when value is not string =>
                values.Cast<object?>().Select(NormalizeJsonInput).ToArray(),
            _ => value,
        };

    // Legacy PS dynamic parameters: one per template parameter, "FromTemplate" suffix on a clash with a cmdlet parameter,
    // HelpMessage keeps the template name. PowerShell prompts for the mandatory ones.
    public static RuntimeDefinedParameterDictionary CreateTemplateParameters(
        PSCmdlet cmdlet, Hashtable? templateObject, string? templateFile, string? templateUri, string? templateSpecId,
        string? queryString, Hashtable? parametersObject, string? parametersFile, string? parametersUri)
    {
        RuntimeDefinedParameterDictionary result = new();
        object? template = NormalizeJsonInput(templateObject);
        if (template is null && string.IsNullOrWhiteSpace(templateFile) && string.IsNullOrWhiteSpace(templateUri)
            && !string.IsNullOrWhiteSpace(templateSpecId))
        {
            try
            {
                template = ReadTemplateSpec(cmdlet, templateSpecId);
            }
            catch (Exception)
            {
                // Like the other sources: no dynamic parameters until the template can be read.
            }
        }
        HashSet<string> staticNames = new(
            new[] { cmdlet.GetType(), typeof(CommonParameters) }
                .Concat(cmdlet.GetType().GetCustomAttributes(typeof(CmdletAttribute), true)
                    .Cast<CmdletAttribute>().Any(a => a.SupportsShouldProcess) ? [typeof(ShouldProcessParameters)] : [])
                .SelectMany(t => t.GetProperties())
                .Where(p => p.IsDefined(typeof(ParameterAttribute), true))
                .Select(p => p.Name),
            StringComparer.OrdinalIgnoreCase);
        foreach ((string name, string? type, bool required, int? minLength, int? maxLength) in
            NewManagementGroupDeploymentProcessor_Orchestrator.GetPowerShellTemplateParameters(
                template, ResolvePath(cmdlet, templateFile), templateUri, queryString,
                NormalizeJsonInput(parametersObject), ResolvePath(cmdlet, parametersFile), parametersUri))
        {
            RuntimeDefinedParameter parameter = new(
                staticNames.Contains(name) ? name + "FromTemplate" : name,
                type?.ToLowerInvariant() switch
                {
                    "string" => typeof(string),
                    "int" => typeof(int),
                    "securestring" => typeof(SecureString),
                    "bool" => typeof(bool),
                    "object" or "secureobject" => typeof(Hashtable),
                    "array" => typeof(object[]),
                    _ => typeof(object),
                },
                [new ParameterAttribute { Mandatory = required, ValueFromPipelineByPropertyName = true, HelpMessage = name }]);
            if (minLength is int min && maxLength is int max)
            {
                parameter.Attributes.Add(new ValidateLengthAttribute(min, max));
            }
            result[parameter.Name] = parameter;
        }
        return result;
    }

    // Legacy PS GetTemplateParameterObject: the template parameters given on the command line override the other sources;
    // passed as KEY=JSON overlays. Legacy sends only the link when -TemplateParameterUri is used.
    public static void AddTemplateParameterValues(
        Dictionary<string, object> parameters, RuntimeDefinedParameterDictionary? templateParameters, string[]? parameter, bool usesParametersUri)
    {
        string[] values = (templateParameters?.Values ?? Enumerable.Empty<RuntimeDefinedParameter>())
            .Where(p => p.IsSet)
            .Select(p => $"{((ParameterAttribute)p.Attributes[0]).HelpMessage}={JsonSerializer.Serialize(NormalizeJsonInput(p.Value))}")
            .ToArray();
        if (values.Length > 0 && !usesParametersUri)
        {
            parameters["Parameter"] = (parameter ?? []).Concat(values).ToArray();
        }
    }

    private static string? ResolvePath(PSCmdlet cmdlet, string? path) =>
        string.IsNullOrWhiteSpace(path) ? path : cmdlet.SessionState.Path.GetUnresolvedProviderPathFromPSPath(path);

    // Legacy PS output type: PSDeployment shown with the legacy list view properties.
    private static object ToPowerShellOutput(object value)
    {
        if (value is PSDeployment deployment)
        {
            PSObject output = PSObject.AsPSObject(deployment);
            output.Members.Add(new PSMemberSet(
                "PSStandardMembers",
                [new PSPropertySet("DefaultDisplayPropertySet", s_deploymentView)]));
            return output;
        }
        return value is IEnumerable<PSDeployment> deployments ? deployments.Select(d => ToPowerShellOutput(d)).ToList() : value;
    }

    // Legacy PS -AsJob: run the command in the background and return the job right away.
    public static void RunAsJob(
        PSCmdlet cmdlet,
        IEngine engine,
        string command,
        Dictionary<string, object> parameters,
        object? passThru = null)
    {
        ManagementGroupDeploymentJob job = new(cmdlet.MyInvocation.Line, $"Long Running Operation for '{cmdlet.MyInvocation.InvocationName}'");
        cmdlet.JobRepository.Add(job);
        EnsureDeploymentFormat(cmdlet);
        cmdlet.WriteObject(job);
        job.Start(token =>
        {
            Run(cmdlet, engine, job.Streams, token, command, parameters);
            if (passThru is not null)
            {
                job.Output.Add(PSObject.AsPSObject(passThru));
            }
        });
    }

    public static (string ManagementGroupId, string Name) ParseId(string id)
    {
        string[] segments = id.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 8
            || !string.Equals(segments[0], "providers", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[1], "Microsoft.Management", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[2], "managementGroups", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[4], "providers", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[5], "Microsoft.Resources", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[6], "deployments", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Management group deployment Id must match /providers/Microsoft.Management/managementGroups/{managementGroupId}/providers/Microsoft.Resources/deployments/{name}.",
                nameof(id));
        }

        return (segments[3], segments[7]);
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
        EnsureDeploymentFormat(cmdlet);
        OutputStreams output = streams with { WriteOutput = (value, ct) => streams.WriteOutput(ToPowerShellOutput(value), ct) };
        using ISession session = engine.OpenExecutionSession(invocation, output, token);
        session.Run();
    }

    public static object? RunForOutput(
        PSCmdlet cmdlet,
        IEngine engine,
        OutputStreams streams,
        CancellationToken token,
        string command,
        Dictionary<string, object> parameters)
    {
        object? output = null;
        OutputStreams capturingStreams = streams with
        {
            WriteOutput = (value, _) =>
            {
                output = value is SerializedJson json ? json.Value : value;
                return Task.CompletedTask;
            },
        };
        Run(cmdlet, engine, capturingStreams, token, command, parameters);
        return output;
    }
}

[Cmdlet(VerbsCommon.New, "AzManagementGroupDeployment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(
    typeof(PSDeployment),
    typeof(WhatIfOperationResult))]
public class NewManagementGroupDeploymentCmdlet : PSCmdlet, IDynamicParameters
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;
    private RuntimeDefinedParameterDictionary? _templateParameters;

    [Parameter(Mandatory = true)]
    public string ManagementGroupId { get; set; } = string.Empty;

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
    [Parameter(Mandatory = false)] public Hashtable? Tag { get; set; }
    [Parameter(Mandatory = false)] public string? DeploymentDebugLogLevel { get; set; }
    [Parameter(Mandatory = false)]
    [ValidateSet("FullResourcePayloads", "ResourceIdOnly", IgnoreCase = true)]
    public string? WhatIfResultFormat { get; set; }
    [Parameter(Mandatory = false)] public string[]? WhatIfExcludeChangeType { get; set; }
    [Parameter(Mandatory = false)] public SwitchParameter ProceedIfNoChange { get; set; }
    [Parameter(Mandatory = false, HelpMessage = "Run cmdlet in the background")] public SwitchParameter AsJob { get; set; }
    [Parameter(Mandatory = false)] public SwitchParameter SkipTemplateParameterPrompt { get; set; }
    // Legacy ResourceManagerCmdletBase switch; it only affects API version lookup, which deployments do not use.
    [Parameter(Mandatory = false)] public SwitchParameter Pre { get; set; }
    [Parameter(Mandatory = false)] public string? Profile { get; set; }

    public object GetDynamicParameters() =>
        _templateParameters = SkipTemplateParameterPrompt
            ? new RuntimeDefinedParameterDictionary()
            : ManagementGroupDeploymentCmdletHelpers.CreateTemplateParameters(
                this, TemplateObject, TemplateFile, TemplateUri, TemplateSpecId, QueryString,
                TemplateParameterObject, TemplateParameterFile, TemplateParameterUri);

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(
            this,
            "ManagementGroupDeploymentError",
            ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        bool armWhatIf = MyInvocation.BoundParameters.TryGetValue("WhatIf", out object? whatIf)
            ? LanguagePrimitives.IsTrue(whatIf)
            : LanguagePrimitives.IsTrue(SessionState.PSVariable.GetValue("WhatIfPreference"));
        Dictionary<string, object> parameters = new()
        {
            [nameof(ManagementGroupId)] = ManagementGroupId,
            [nameof(Location)] = Location,
        };
        AddBound(parameters, nameof(Name), Name);
        AddBound(parameters, nameof(TemplateFile), TemplateFile);
        AddBound(parameters, nameof(TemplateUri), TemplateUri);
        AddBound(parameters, nameof(TemplateSpecId), TemplateSpecId);
        AddBound(
            parameters,
            nameof(TemplateObject),
            ManagementGroupDeploymentCmdletHelpers.NormalizeJsonInput(TemplateObject));
        AddBound(parameters, nameof(QueryString), QueryString);
        AddBound(parameters, nameof(TemplateParameterFile), TemplateParameterFile);
        AddBound(parameters, nameof(TemplateParameterUri), TemplateParameterUri);
        AddBound(
            parameters,
            nameof(TemplateParameterObject),
            ManagementGroupDeploymentCmdletHelpers.NormalizeJsonInput(TemplateParameterObject));
        AddBound(parameters, nameof(Parameter), Parameter);
        ManagementGroupDeploymentCmdletHelpers.AddTemplateParameterValues(
            parameters, _templateParameters, Parameter, MyInvocation.BoundParameters.ContainsKey(nameof(TemplateParameterUri)));
        AddBound(parameters, nameof(ValidationLevel), ValidationLevel);
        AddBound(parameters, nameof(DeploymentDebugLogLevel), DeploymentDebugLogLevel);
        AddBound(parameters, nameof(WhatIfResultFormat), WhatIfResultFormat);
        AddBound(parameters, nameof(WhatIfExcludeChangeType), WhatIfExcludeChangeType);
        AddBound(parameters, nameof(Profile), Profile);
        if (Tag is not null)
        {
            parameters[nameof(Tag)] = Tag.Cast<DictionaryEntry>()
                .ToDictionary(
                    entry => entry.Key.ToString()!,
                    entry => entry.Value?.ToString() ?? string.Empty);
        }
        if (armWhatIf)
        {
            parameters["WhatIf"] = true;
            ManagementGroupDeploymentCmdletHelpers.Run(
                this,
                engine,
                streams,
                _cts.Token,
                "New-AzManagementGroupDeployment",
                parameters);
            return;
        }

        bool confirm = MyInvocation.BoundParameters.TryGetValue("Confirm", out object? confirmValue)
            ? LanguagePrimitives.IsTrue(confirmValue)
            : SessionState.PSVariable.GetValue("ConfirmPreference") is ConfirmImpact preference
                && preference <= ConfirmImpact.Medium;
        bool skipConfirmation = false;
        if (confirm)
        {
            parameters["WhatIf"] = true;
            object? preview = ManagementGroupDeploymentCmdletHelpers.RunForOutput(
                this,
                engine,
                streams,
                _cts.Token,
                "New-AzManagementGroupDeployment",
                parameters);
            WriteInformation(preview, ["PSHOST"]);
            parameters.Remove("WhatIf");
            skipConfirmation = ProceedIfNoChange.IsPresent
                && preview is WhatIfOperationResult result
                && !Microsoft.Azure.Azclips.Extensions.Resources.Processors
                    .NewManagementGroupDeploymentProcessor_Orchestrator.HasChanges(result);
        }

        string target =
            $"management group '{ManagementGroupId}' deployment '{Name ?? "(generated name)"}'";
        if (!skipConfirmation && !ShouldProcess(target, "Create"))
        {
            return;
        }

        if (AsJob)
        {
            ManagementGroupDeploymentCmdletHelpers.RunAsJob(this, engine, "New-AzManagementGroupDeployment", parameters);
            return;
        }

        ManagementGroupDeploymentCmdletHelpers.Run(
            this,
            engine,
            streams,
            _cts.Token,
            "New-AzManagementGroupDeployment",
            parameters);
    }

    private void AddBound(Dictionary<string, object> parameters, string name, object? value)
    {
        if (MyInvocation.BoundParameters.ContainsKey(name) && value is not null)
        {
            parameters[name] = value;
        }
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}

[Cmdlet(VerbsDiagnostic.Test, "AzManagementGroupDeployment")]
public class TestManagementGroupDeploymentCmdlet : PSCmdlet, IDynamicParameters
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;
    private RuntimeDefinedParameterDictionary? _templateParameters;

    [Parameter(Mandatory = true)] public string ManagementGroupId { get; set; } = string.Empty;
    [Alias("DeploymentName")]
    [Parameter(Mandatory = false)] public string? Name { get; set; }
    [Parameter(Mandatory = true)] public string Location { get; set; } = string.Empty;
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
    [Parameter(Mandatory = false)] public SwitchParameter SuppressDiagnostics { get; set; }
    [Parameter(Mandatory = false)] public SwitchParameter SkipTemplateParameterPrompt { get; set; }
    // Legacy ResourceManagerCmdletBase switch; it only affects API version lookup, which deployments do not use.
    [Parameter(Mandatory = false)] public SwitchParameter Pre { get; set; }
    [Parameter(Mandatory = false)] public string? Profile { get; set; }

    public object GetDynamicParameters() =>
        _templateParameters = SkipTemplateParameterPrompt
            ? new RuntimeDefinedParameterDictionary()
            : ManagementGroupDeploymentCmdletHelpers.CreateTemplateParameters(
                this, TemplateObject, TemplateFile, TemplateUri, TemplateSpecId, QueryString,
                TemplateParameterObject, TemplateParameterFile, TemplateParameterUri);

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(
            this,
            "ManagementGroupDeploymentValidationError",
            ErrorCategory.InvalidData);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        Dictionary<string, object> parameters = new()
        {
            [nameof(ManagementGroupId)] = ManagementGroupId,
            [nameof(Location)] = Location,
        };
        foreach (string name in new[]
        {
            nameof(Name), nameof(TemplateFile), nameof(TemplateUri), nameof(TemplateSpecId),
            nameof(QueryString), nameof(TemplateParameterFile),
            nameof(TemplateParameterUri), nameof(Parameter),
            nameof(ValidationLevel), nameof(Profile),
        })
        {
            if (MyInvocation.BoundParameters.TryGetValue(name, out object? value) && value is not null)
            {
                parameters[name] = value;
            }
        }
        if (SuppressDiagnostics)
        {
            parameters[nameof(SuppressDiagnostics)] = true;
        }
        if (TemplateObject is not null)
        {
            parameters[nameof(TemplateObject)] =
                ManagementGroupDeploymentCmdletHelpers.NormalizeJsonInput(TemplateObject)!;
        }
        if (TemplateParameterObject is not null)
        {
            parameters[nameof(TemplateParameterObject)] =
                ManagementGroupDeploymentCmdletHelpers.NormalizeJsonInput(TemplateParameterObject)!;
        }
        ManagementGroupDeploymentCmdletHelpers.AddTemplateParameterValues(
            parameters, _templateParameters, Parameter, MyInvocation.BoundParameters.ContainsKey(nameof(TemplateParameterUri)));

        ManagementGroupDeploymentCmdletHelpers.Run(
            this,
            engine,
            streams,
            _cts.Token,
            "Test-AzManagementGroupDeployment",
            parameters);
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}

[Cmdlet(VerbsCommon.Get, "AzManagementGroupDeployment", DefaultParameterSetName = NameSet)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Processors.PSDeployment")]
public class GetManagementGroupDeploymentCmdlet : PSCmdlet
{
    private const string NameSet = "GetByName";
    private const string IdSet = "GetById";
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Position = 0, Mandatory = true, ParameterSetName = NameSet)]
    public string? ManagementGroupId { get; set; }

    [Alias("DeploymentName")]
    [Parameter(Position = 1, Mandatory = false, ParameterSetName = NameSet)]
    public string? Name { get; set; }

    [Alias("DeploymentId", "ResourceId")]
    [Parameter(Mandatory = true, ParameterSetName = IdSet)]
    public string? Id { get; set; }

    // Legacy ResourceManagerCmdletBase switch; it only affects API version lookup, which deployments do not use.
    [Parameter(Mandatory = false)]
    public SwitchParameter Pre { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(
            this,
            "ManagementGroupDeploymentError",
            ErrorCategory.NotSpecified,
            enumerateOutput: true);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        string managementGroupId;
        string? name;
        if (ParameterSetName == IdSet)
        {
            (managementGroupId, string parsedName) =
                ManagementGroupDeploymentCmdletHelpers.ParseId(Id!);
            name = parsedName;
        }
        else
        {
            managementGroupId = ManagementGroupId!;
            name = Name;
        }

        Dictionary<string, object> parameters = new()
        {
            [nameof(ManagementGroupId)] = managementGroupId,
        };
        string command = "Get-AzManagementGroupDeployment_List";
        if (!string.IsNullOrWhiteSpace(name))
        {
            parameters[nameof(Name)] = name;
            command = "Get-AzManagementGroupDeployment_Get";
        }
        if (Profile is not null)
        {
            parameters[nameof(Profile)] = Profile;
        }

        ManagementGroupDeploymentCmdletHelpers.Run(
            this,
            engine,
            streams,
            _cts.Token,
            command,
            parameters);
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}

[Cmdlet(VerbsLifecycle.Stop, "AzManagementGroupDeployment", DefaultParameterSetName = NameSet, SupportsShouldProcess = true)]
[OutputType(typeof(bool))]
public class StopManagementGroupDeploymentCmdlet : PSCmdlet
{
    private const string NameSet = "StopByName";
    private const string IdSet = "StopById";
    private const string ObjectSet = "StopByInputObject";
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Position = 0, Mandatory = true, ParameterSetName = NameSet)]
    public string? ManagementGroupId { get; set; }
    [Alias("DeploymentName")]
    [Parameter(Position = 1, Mandatory = true, ParameterSetName = NameSet)]
    public string? Name { get; set; }
    [Alias("DeploymentId", "ResourceId")]
    [Parameter(Mandatory = true, ParameterSetName = IdSet)]
    public string? Id { get; set; }
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ObjectSet)]
    public PSDeployment? InputObject { get; set; }
    [Parameter(Mandatory = false)] public SwitchParameter PassThru { get; set; }
    // Legacy ResourceManagerCmdletBase switch; it only affects API version lookup, which deployments do not use.
    [Parameter(Mandatory = false)] public SwitchParameter Pre { get; set; }
    [Parameter(Mandatory = false)] public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(
            this,
            "ManagementGroupDeploymentError",
            ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        (string managementGroupId, string name) = ResolveTarget();
        if (!ShouldProcess(
            $"management group '{managementGroupId}' deployment '{name}'",
            "Cancel"))
        {
            return;
        }

        Dictionary<string, object> parameters = new()
        {
            [nameof(ManagementGroupId)] = managementGroupId,
            [nameof(Name)] = name,
        };
        if (Profile is not null)
        {
            parameters[nameof(Profile)] = Profile;
        }
        ManagementGroupDeploymentCmdletHelpers.Run(
            this,
            engine,
            streams,
            _cts.Token,
            "Stop-AzManagementGroupDeployment",
            parameters);
        if (PassThru)
        {
            WriteObject(true);
        }
    }

    private (string ManagementGroupId, string Name) ResolveTarget()
    {
        if (Id is not null)
        {
            return ManagementGroupDeploymentCmdletHelpers.ParseId(Id);
        }
        if (InputObject?.Id is not null)
        {
            return ManagementGroupDeploymentCmdletHelpers.ParseId(InputObject.Id);
        }
        return (ManagementGroupId!, Name!);
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}

[Cmdlet(VerbsCommon.Remove, "AzManagementGroupDeployment", DefaultParameterSetName = NameSet, SupportsShouldProcess = true)]
[OutputType(typeof(bool))]
public class RemoveManagementGroupDeploymentCmdlet : PSCmdlet
{
    private const string NameSet = "RemoveByName";
    private const string IdSet = "RemoveById";
    private const string ObjectSet = "RemoveByInputObject";
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Position = 0, Mandatory = true, ParameterSetName = NameSet)]
    public string? ManagementGroupId { get; set; }
    [Alias("DeploymentName")]
    [Parameter(Position = 1, Mandatory = true, ParameterSetName = NameSet)]
    public string? Name { get; set; }
    [Alias("DeploymentId", "ResourceId")]
    [Parameter(Mandatory = true, ParameterSetName = IdSet)]
    public string? Id { get; set; }
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ObjectSet)]
    public PSDeployment? InputObject { get; set; }
    [Parameter(Mandatory = false)] public SwitchParameter PassThru { get; set; }
    [Parameter(Mandatory = false, HelpMessage = "Run cmdlet in the background")] public SwitchParameter AsJob { get; set; }
    // Legacy ResourceManagerCmdletBase switch; it only affects API version lookup, which deployments do not use.
    [Parameter(Mandatory = false)] public SwitchParameter Pre { get; set; }
    [Parameter(Mandatory = false)] public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(
            this,
            "ManagementGroupDeploymentError",
            ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        (string managementGroupId, string name) = ResolveTarget();
        if (!ShouldProcess(
            $"management group '{managementGroupId}' deployment '{name}'",
            "Remove"))
        {
            return;
        }

        Dictionary<string, object> parameters = new()
        {
            [nameof(ManagementGroupId)] = managementGroupId,
            [nameof(Name)] = name,
        };
        if (Profile is not null)
        {
            parameters[nameof(Profile)] = Profile;
        }
        if (AsJob)
        {
            ManagementGroupDeploymentCmdletHelpers.RunAsJob(
                this, engine, "Remove-AzManagementGroupDeployment", parameters, PassThru ? true : null);
            return;
        }
        ManagementGroupDeploymentCmdletHelpers.Run(
            this,
            engine,
            streams,
            _cts.Token,
            "Remove-AzManagementGroupDeployment",
            parameters);
        if (PassThru)
        {
            WriteObject(true);
        }
    }

    private (string ManagementGroupId, string Name) ResolveTarget()
    {
        if (Id is not null)
        {
            return ManagementGroupDeploymentCmdletHelpers.ParseId(Id);
        }
        if (InputObject?.Id is not null)
        {
            return ManagementGroupDeploymentCmdletHelpers.ParseId(InputObject.Id);
        }
        return (ManagementGroupId!, Name!);
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}

/// <summary>Legacy PS AzureLongRunningJob: the engine run on a background thread, its streams kept in the job.</summary>
internal sealed class ManagementGroupDeploymentJob : Job
{
    private readonly CancellationTokenSource _cts = new();

    public ManagementGroupDeploymentJob(string command, string name)
        : base(command, name)
    {
        PSJobTypeName = "AzureLongRunningJob";
        Streams = new OutputStreams(
            WriteOutput: (value, _) =>
            {
                value = value is SerializedJson json ? json.Value : value;
                foreach (object? item in value is IEnumerable items and not string ? items.Cast<object?>() : [value])
                {
                    if (item is not null)
                    {
                        Output.Add(PSObject.AsPSObject(item));
                    }
                }
                return Task.CompletedTask;
            },
            WriteVerbose: (value, _) => { Verbose.Add(new VerboseRecord(value?.ToString() ?? string.Empty)); return Task.CompletedTask; },
            WriteDebug: (value, _) => { Debug.Add(new DebugRecord(value?.ToString() ?? string.Empty)); return Task.CompletedTask; },
            WriteWarning: (value, _) => { Warning.Add(new WarningRecord(value?.ToString() ?? string.Empty)); return Task.CompletedTask; },
            WriteError: (value, _) =>
            {
                Error.Add(new ErrorRecord(new Exception(value?.ToString() ?? string.Empty), "ManagementGroupDeploymentError", ErrorCategory.NotSpecified, null));
                return Task.CompletedTask;
            },
            WriteInformation: (value, _) => { Information.Add(new InformationRecord(value, null)); return Task.CompletedTask; });
    }

    public OutputStreams Streams { get; }

    public override string StatusMessage => string.Empty;

    public override bool HasMoreData =>
        Output.Count > 0 || Error.Count > 0 || Warning.Count > 0 || Verbose.Count > 0 || Debug.Count > 0 || Information.Count > 0;

    public override string Location => "localhost";

    public void Start(Action<CancellationToken> run)
    {
        SetJobState(JobState.Running);
        Task.Run(() =>
        {
            try
            {
                run(_cts.Token);
                SetJobState(_cts.IsCancellationRequested ? JobState.Stopped : Error.Count > 0 ? JobState.Failed : JobState.Completed);
            }
            catch (Exception ex)
            {
                Error.Add(new ErrorRecord(ex, "ManagementGroupDeploymentError", ErrorCategory.NotSpecified, null));
                SetJobState(_cts.IsCancellationRequested ? JobState.Stopped : JobState.Failed);
            }
        });
    }

    public override void StopJob() => _cts.Cancel();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Dispose();
        }
        base.Dispose(disposing);
    }
}
