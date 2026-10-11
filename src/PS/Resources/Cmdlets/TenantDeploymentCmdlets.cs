// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Collections.Concurrent;
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

/// <summary>Shared engine plumbing for the *-AzTenantDeployment cmdlets: forwards bound parameters to the Resources extension.</summary>
public abstract class TenantDeploymentCmdletBase : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected virtual bool EnumerateOutput => false;

    // Legacy ResourceManager.format.ps1xml PSDeployment list view. The module ships no ps1xml, so it is loaded at run time.
    private const string DeploymentFormat = """
        <?xml version="1.0" encoding="utf-8"?>
        <Configuration><ViewDefinitions><View>
          <Name>Microsoft.Azure.Azclips.Extensions.Resources.Processors.PSDeployment</Name>
          <ViewSelectedBy><TypeName>Microsoft.Azure.Azclips.Extensions.Resources.Processors.PSDeployment</TypeName></ViewSelectedBy>
          <ListControl><ListEntries><ListEntry><ListItems>
            <ListItem><Label>Id</Label><PropertyName>Id</PropertyName></ListItem>
            <ListItem><Label>DeploymentName</Label><PropertyName>DeploymentName</PropertyName></ListItem>
            <ListItem><ItemSelectionCondition><ScriptBlock>$_.ResourceGroupName -ne $null</ScriptBlock></ItemSelectionCondition><Label>ResourceGroupName</Label><PropertyName>ResourceGroupName</PropertyName></ListItem>
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

    // Fallback when format data can't be loaded (e.g. blocked by policy): same properties, raw names.
    private static readonly string[] s_deploymentView =
    [
        "Id", "DeploymentName", "Location", "ProvisioningState", "Timestamp", "Mode",
        "TemplateLinkString", "ParametersString", "OutputsString", "DeploymentDebugLogLevel",
    ];

    private static readonly ConcurrentDictionary<Guid, bool> s_formatRunspaces = new();

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "TenantDeploymentError", ErrorCategory.NotSpecified, enumerateOutput: EnumerateOutput);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
        EnsureDeploymentFormat();
    }

    private void EnsureDeploymentFormat()
    {
        if (Runspace.DefaultRunspace is not { } runspace || !s_formatRunspaces.TryAdd(runspace.InstanceId, true))
        {
            return;
        }
        try
        {
            string path = Path.Combine(Path.GetTempPath(), "Azclips.Resources.PSDeployment.format.ps1xml");
            if (!File.Exists(path) || File.ReadAllText(path) != DeploymentFormat)
            {
                File.WriteAllText(path, DeploymentFormat);
            }
            InvokeCommand.InvokeScript(false, ScriptBlock.Create("Update-FormatData -PrependPath $args[0]"), null, path);
        }
        catch (Exception)
        {
            // DefaultDisplayPropertySet (ToPowerShellOutput) is used instead.
        }
    }

    private static object ToPowerShellOutput(object value)
    {
        if (value is PSDeployment deployment)
        {
            PSObject output = PSObject.AsPSObject(deployment);
            output.Members.Add(new PSMemberSet("PSStandardMembers", [new PSPropertySet("DefaultDisplayPropertySet", s_deploymentView)]));
            return output;
        }
        return value is IEnumerable<PSDeployment> deployments ? deployments.Select(d => ToPowerShellOutput(d)).ToList() : value;
    }

    protected Dictionary<string, object> GetBoundParameters() =>
        MyInvocation.BoundParameters
            .Where(p => !CommonParameters.Contains(p.Key) && !OptionalCommonParameters.Contains(p.Key))
            .ToDictionary(p => p.Key, p => p.Value is SwitchParameter s ? s.IsPresent : p.Value);

    protected void Invoke(string commandName, Dictionary<string, object> parameters) => Invoke(commandName, parameters, streams);

    // Legacy runs the what-if in process and formats it for ShouldProcess; capture the engine output instead of writing it.
    // Legacy DeploymentWhatIfCmdlet.ExecuteWhatIf shows a status line and clears it before the result or error is written.
    protected object? InvokeForOutput(string commandName, Dictionary<string, object> parameters)
    {
        const string statusMessage = "Getting the latest status of all resources...";
        object? output = null;
        List<(object Value, CancellationToken Token)> errors = [];
        WriteInformation(new HostInformationMessage { Message = statusMessage, NoNewLine = true }, ["PSHOST"]);
        try
        {
            Invoke(commandName, parameters, streams with
            {
                WriteOutput = (value, _) =>
                {
                    output = value is SerializedJson json ? json.Value : value;
                    return Task.CompletedTask;
                },
                WriteError = (value, token) =>
                {
                    errors.Add((value, token));
                    return Task.CompletedTask;
                },
            });
        }
        finally
        {
            WriteInformation(new HostInformationMessage { Message = $"\r{new string(' ', statusMessage.Length)}\r", NoNewLine = true }, ["PSHOST"]);
            foreach ((object value, CancellationToken token) in errors)
            {
                streams.WriteError(value, token).GetAwaiter().GetResult();
            }
        }
        return output;
    }

    // Legacy -AsJob (AzureLongRunningJob): run the engine in the background and return the job right away.
    protected void InvokeAsJob(string commandName, Dictionary<string, object> parameters, object? passThru = null)
    {
        TenantDeploymentJob job = new(MyInvocation.Line, $"Long Running Operation for '{MyInvocation.InvocationName}'");
        JobRepository.Add(job);
        WriteObject(job);
        job.Start(token =>
        {
            Invoke(commandName, parameters, job.Streams, token);
            if (passThru is not null)
            {
                job.Output.Add(PSObject.AsPSObject(passThru));
            }
        });
    }

    private void Invoke(string commandName, Dictionary<string, object> parameters, OutputStreams outputStreams) =>
        Invoke(commandName, parameters, outputStreams, _cts.Token);

    private void Invoke(string commandName, Dictionary<string, object> parameters, OutputStreams outputStreams, CancellationToken token)
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var invocation = new CommandInvocation("Resources", commandName, parameters, false, MyInvocation.MyCommand.ModuleName, MyInvocation.MyCommand.Version?.ToString());
        telemetryService.TagInvocation(invocation);

        OutputStreams output = outputStreams with { WriteOutput = (value, ct) => outputStreams.WriteOutput(ToPowerShellOutput(value), ct) };
        using ISession session = engine.OpenExecutionSession(invocation, output, token);
        session.Run();
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();

    // Legacy ResourceIdUtility.GetDeploymentName: resource names after the last "providers/{namespace}" segment.
    protected static string GetDeploymentName(string id)
    {
        var segments = id.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var last = Array.FindLastIndex(segments, s => s.Equals("providers", StringComparison.OrdinalIgnoreCase));
        return last < 0 ? "" : string.Join("/", segments.Skip(last + 2).Where((_, i) => i % 2 != 0));
    }
}

/// <summary>
/// Shared New-/Test-AzTenantDeployment input surface: legacy DeploymentCmdletBase parameter sets, .bicepparam checks
/// and one dynamic parameter per template parameter.
/// </summary>
public abstract class TenantDeploymentTemplateCmdletBase : TenantDeploymentCmdletBase, IDynamicParameters
{
    protected const string TemplateObjectParameterObjectParameterSetName = "ByTemplateObjectAndParameterObject";
    protected const string TemplateObjectParameterFileParameterSetName = "ByTemplateObjectAndParameterFile";
    protected const string TemplateObjectParameterUriParameterSetName = "ByTemplateObjectAndParameterUri";
    protected const string TemplateFileParameterObjectParameterSetName = "ByTemplateFileAndParameterObject";
    protected const string TemplateFileParameterFileParameterSetName = "ByTemplateFileAndParameterFile";
    protected const string TemplateFileParameterUriParameterSetName = "ByTemplateFileAndParameterUri";
    protected const string TemplateUriParameterObjectParameterSetName = "ByTemplateUriAndParameterObject";
    protected const string TemplateUriParameterFileParameterSetName = "ByTemplateUriAndParameterFile";
    protected const string TemplateUriParameterUriParameterSetName = "ByTemplateUriAndParameterUri";
    protected const string ParameterlessTemplateObjectParameterSetName = "ByTemplateObjectWithNoParameters";
    protected const string ParameterlessTemplateFileParameterSetName = "ByTemplateFileWithNoParameters";
    protected const string ParameterlessTemplateUriParameterSetName = "ByTemplateUriWithNoParameters";
    protected const string TemplateSpecResourceIdParameterSetName = "ByTemplateSpecResourceId";
    protected const string TemplateSpecResourceIdParameterFileParameterSetName = "ByTemplateSpecResourceIdAndParams";
    protected const string TemplateSpecResourceIdParameterUriParameterSetName = "ByTemplateSpecResourceIdAndParamsUri";
    protected const string TemplateSpecResourceIdParameterObjectParameterSetName = "ByTemplateSpecResourceIdAndParamsObject";
    protected const string ByParameterFileWithNoTemplateParameterSetName = "ByParameterFileWithNoTemplate";

    private RuntimeDefinedParameterDictionary _templateParameters = new();

    [Parameter(Mandatory = true, HelpMessage = "The location to store deployment data.")]
    [ValidateNotNullOrEmpty]
    public string Location { get; set; } = "";

    [Parameter(ParameterSetName = TemplateObjectParameterObjectParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "A hash table which represents the parameters.")]
    [Parameter(ParameterSetName = TemplateFileParameterObjectParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "A hash table which represents the parameters.")]
    [Parameter(ParameterSetName = TemplateUriParameterObjectParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "A hash table which represents the parameters.")]
    [Parameter(ParameterSetName = TemplateSpecResourceIdParameterObjectParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "A hash table which represents the parameters.")]
    public Hashtable? TemplateParameterObject { get; set; }

    [Parameter(ParameterSetName = TemplateObjectParameterFileParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Parameter file to use for the template.")]
    [Parameter(ParameterSetName = TemplateFileParameterFileParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Parameter file to use for the template.")]
    [Parameter(ParameterSetName = TemplateUriParameterFileParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Parameter file to use for the template.")]
    [Parameter(ParameterSetName = TemplateSpecResourceIdParameterFileParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Parameter file to use for the template.")]
    [Parameter(ParameterSetName = ByParameterFileWithNoTemplateParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Parameter file to use for the template.")]
    [ValidateNotNullOrEmpty]
    public string? TemplateParameterFile { get; set; }

    [Parameter(ParameterSetName = TemplateObjectParameterUriParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Uri to the template parameter file.")]
    [Parameter(ParameterSetName = TemplateFileParameterUriParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Uri to the template parameter file.")]
    [Parameter(ParameterSetName = TemplateUriParameterUriParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Uri to the template parameter file.")]
    [Parameter(ParameterSetName = TemplateSpecResourceIdParameterUriParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Uri to the template parameter file.")]
    [ValidateNotNullOrEmpty]
    public string? TemplateParameterUri { get; set; }

    [Parameter(ParameterSetName = TemplateObjectParameterFileParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "A hash table which represents the template.")]
    [Parameter(ParameterSetName = TemplateObjectParameterObjectParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "A hash table which represents the template.")]
    [Parameter(ParameterSetName = TemplateObjectParameterUriParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "A hash table which represents the template.")]
    [Parameter(ParameterSetName = ParameterlessTemplateObjectParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "A hash table which represents the template.")]
    [ValidateNotNull]
    public Hashtable? TemplateObject { get; set; }

    [Parameter(ParameterSetName = TemplateFileParameterObjectParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Local path to the template file. Supported template file type: json and bicep.")]
    [Parameter(ParameterSetName = TemplateFileParameterFileParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true)]
    [Parameter(ParameterSetName = TemplateFileParameterUriParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true)]
    [Parameter(ParameterSetName = ParameterlessTemplateFileParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string? TemplateFile { get; set; }

    [Parameter(ParameterSetName = TemplateUriParameterObjectParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Uri to the template file.")]
    [Parameter(ParameterSetName = TemplateUriParameterFileParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true)]
    [Parameter(ParameterSetName = TemplateUriParameterUriParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true)]
    [Parameter(ParameterSetName = ParameterlessTemplateUriParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string? TemplateUri { get; set; }

    [Parameter(ParameterSetName = TemplateSpecResourceIdParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Resource ID of the templateSpec to be deployed.")]
    [Parameter(ParameterSetName = TemplateSpecResourceIdParameterUriParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Resource ID of the templateSpec to be deployed.")]
    [Parameter(ParameterSetName = TemplateSpecResourceIdParameterFileParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Resource ID of the templateSpec to be deployed.")]
    [Parameter(ParameterSetName = TemplateSpecResourceIdParameterObjectParameterSetName, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Resource ID of the templateSpec to be deployed.")]
    [ValidateNotNullOrEmpty]
    public string? TemplateSpecId { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Skips the PowerShell dynamic parameter processing that checks if the provided template parameter contains all necessary parameters used by the template. " +
                                                "This check would prompt the user to provide a value for the missing parameters, but providing the -SkipTemplateParameterPrompt will ignore this prompt and " +
                                                "error out immediately if a parameter was found not to be bound in the template. For non-interactive scripts, -SkipTemplateParameterPrompt can be provided " +
                                                "to provide a better error message in the case where not all required parameters are satisfied.")]
    public SwitchParameter SkipTemplateParameterPrompt { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The query string (for example, a SAS token) to be used with the TemplateUri parameter. Would be used in case of linked templates")]
    public string? QueryString { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Sets the validation level for validate/what-if. ValidationLevel can be Template(Skips provider validation), Provider(Performs full validation), " +
                                                "or ProviderNoRbac(Performs full validation using RBAC read checks instead of RBAC write checks for provider validation).")]
    public string? ValidationLevel { get; set; }

    // Legacy ResourceManagerCmdletBase switch; it only affects API version lookup, which deployments do not use.
    [Parameter(Mandatory = false, HelpMessage = "When set, indicates that the cmdlet should use pre-release API versions when automatically determining which version to use.")]
    public SwitchParameter Pre { get; set; }

    protected abstract string CommandName { get; }

    // Legacy DeploymentCmdletBase.GetDynamicParameters. PowerShell reports a throw here as
    // "Cannot retrieve the dynamic parameters for the cmdlet. ..." like legacy.
    public object GetDynamicParameters()
    {
        bool isBicepParam = IsExtension(TemplateParameterFile, ".bicepparam");
        DynamicParameterValidityCheck(isBicepParam);
        if (isBicepParam)
        {
            ResolvePath(TemplateParameterFile);
        }
        if (IsExtension(TemplateFile, ".bicep"))
        {
            ResolvePath(TemplateFile);
        }

        _templateParameters = new RuntimeDefinedParameterDictionary();
        if (SkipTemplateParameterPrompt)
        {
            return _templateParameters;
        }

        HashSet<string> staticNames = new(
            new[] { GetType(), typeof(CommonParameters) }
                .Concat(GetType().GetCustomAttributes(typeof(CmdletAttribute), true)
                    .Cast<CmdletAttribute>().Any(a => a.SupportsShouldProcess) ? [typeof(ShouldProcessParameters)] : [])
                .SelectMany(t => t.GetProperties())
                .Where(p => p.IsDefined(typeof(ParameterAttribute), true))
                .Select(p => p.Name),
            StringComparer.OrdinalIgnoreCase);
        // Legacy reads the template from TemplateUri + "?" + QueryString; New (DeploymentCreateCmdlet) skips the "?" if already present.
        string? templateUri = string.IsNullOrEmpty(QueryString) ? TemplateUri
            : this is NewTenantDeploymentCmdlet && QueryString.StartsWith('?') ? TemplateUri + QueryString
            : TemplateUri + "?" + QueryString;
        foreach ((string name, string? type, bool required, int? minLength, int? maxLength) in
            TestTenantDeploymentProcessor_Orchestrator.GetPowerShellTemplateParameters(
                NormalizeJsonInput(TemplateObject), ResolvePath(TemplateFile), templateUri, TemplateSpecId, ReadTemplateSpec,
                NormalizeJsonInput(TemplateParameterObject), ResolvePath(TemplateParameterFile), TemplateParameterUri))
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
            _templateParameters[parameter.Name] = parameter;
        }
        return _templateParameters;
    }

    protected override void ProcessRecord() => Invoke(CommandName, BuildEngineParameters());

    protected Dictionary<string, object> BuildEngineParameters()
    {
        var parameters = GetBoundParameters();
        foreach (string name in _templateParameters.Keys)
        {
            parameters.Remove(name);
        }
        parameters.Remove(nameof(SkipTemplateParameterPrompt));
        parameters.Remove(nameof(Pre));

        // Legacy OnBeginProcessing ResolvePath: a missing file fails with "Cannot find path '...' because it does not exist."
        if (TemplateFile is not null)
        {
            parameters[nameof(TemplateFile)] = ResolvePath(TemplateFile)!;
        }
        if (TemplateParameterFile is not null)
        {
            parameters[nameof(TemplateParameterFile)] = ResolvePath(TemplateParameterFile)!;
        }
        if (TemplateObject is not null)
        {
            parameters[nameof(TemplateObject)] = NormalizeJsonInput(TemplateObject)!;
        }
        if (TemplateParameterObject is not null)
        {
            parameters[nameof(TemplateParameterObject)] = NormalizeJsonInput(TemplateParameterObject)!;
        }

        // Legacy GetUsedDynamicParametersAsDictionary: the bound dynamic parameters, keyed by the template name (HelpMessage).
        string[] values = _templateParameters.Values
            .Where(p => MyInvocation.BoundParameters.ContainsKey(p.Name))
            .Select(p => $"{((ParameterAttribute)p.Attributes[0]).HelpMessage}={JsonSerializer.Serialize(NormalizeJsonInput(MyInvocation.BoundParameters[p.Name]))}")
            .ToArray();
        if (values.Length > 0)
        {
            parameters["TemplateParameterValues"] = values;
        }

        return parameters;
    }

    private void DynamicParameterValidityCheck(bool isBicepParam)
    {
        if (IsExtension(TemplateUri, ".bicep"))
        {
            throw new PSInvalidOperationException($"The -{nameof(TemplateUri)} parameter is not supported with .bicep files. Please download the file and pass it using -{nameof(TemplateFile)}.");
        }
        if (!isBicepParam && string.IsNullOrEmpty(TemplateFile) && string.IsNullOrEmpty(TemplateUri) && string.IsNullOrEmpty(TemplateSpecId) && TemplateObject == null)
        {
            throw new PSInvalidOperationException($"One of the -{nameof(TemplateFile)}, -{nameof(TemplateUri)}, -{nameof(TemplateSpecId)} or -{nameof(TemplateObject)} parameters must be supplied unless a .bicepparam file is supplied with parameter -{nameof(TemplateParameterFile)}.");
        }
        if (isBicepParam && !string.IsNullOrEmpty(TemplateFile) && !IsExtension(TemplateFile, ".bicep"))
        {
            throw new PSInvalidOperationException($"Parameter -{nameof(TemplateFile)} only permits .bicep files if a .bicepparam file is supplied with parameter -{nameof(TemplateParameterFile)}.");
        }
        if (isBicepParam && (!string.IsNullOrEmpty(TemplateUri) || !string.IsNullOrEmpty(TemplateSpecId) || TemplateObject != null))
        {
            throw new PSInvalidOperationException($"Parameters -{nameof(TemplateUri)}, -{nameof(TemplateSpecId)} or -{nameof(TemplateObject)} cannot be used if a .bicepparam file is supplied with parameter -{nameof(TemplateParameterFile)}.");
        }
    }

    // Legacy CmdletExtensions.ResolvePath.
    private string? ResolvePath(string? path) =>
        path is null ? null
            : SessionState.Path.GetResolvedPSPathFromPSPath(path.Trim('"', '\'', ' ')) is { Count: > 0 } result ? result[0].ProviderPath : "";

    private static bool IsExtension(string? path, string extension) =>
        !string.IsNullOrEmpty(path) && Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase);

    private static object? NormalizeJsonInput(object? value) =>
        value switch
        {
            PSObject psObject => NormalizeJsonInput(psObject.BaseObject),
            SecureString secure => new NetworkCredential(string.Empty, secure).Password,
            IDictionary dictionary => dictionary.Cast<DictionaryEntry>().ToDictionary(
                entry => entry.Key.ToString()!,
                entry => NormalizeJsonInput(entry.Value)),
            IEnumerable items when value is not string => items.Cast<object?>().Select(NormalizeJsonInput).ToArray(),
            _ => value,
        };

    // Legacy GetTemplateContentFromTemplateSpec: properties.mainTemplate of the template spec version.
    private object? ReadTemplateSpec(string templateSpecId)
    {
        RestResponse? response = null;
        OutputStreamDelegate ignore = (_, _) => Task.CompletedTask;
        OutputStreams quiet = new((value, _) => { response = value as RestResponse; return Task.CompletedTask; }, ignore, ignore, ignore, ignore, ignore);
        Dictionary<string, object> parameters = new() { ["Method"] = "GET", ["Uri"] = $"{templateSpecId.Trim('"')}?api-version=2022-02-01" };
        if (!string.IsNullOrEmpty(Profile))
        {
            parameters[nameof(Profile)] = Profile;
        }
        var invocation = new CommandInvocation("Resources", "Invoke-AzRestMethod", parameters, false, MyInvocation.MyCommand.ModuleName, MyInvocation.MyCommand.Version?.ToString());
        using ISession session = AzclipsServiceProvider.GetRequiredService<IEngine>().OpenExecutionSession(invocation, quiet, CancellationToken.None);
        session.Run();
        return response is { StatusCode: >= 200 and < 300 } ? JsonNode.Parse(response.Content)?["properties"]?["mainTemplate"] : null;
    }
}

[Cmdlet(VerbsCommon.New, "AzTenantDeployment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium, DefaultParameterSetName = ParameterlessTemplateFileParameterSetName)]
[OutputType(typeof(PSDeployment))]
public class NewTenantDeploymentCmdlet : TenantDeploymentTemplateCmdletBase
{
    private static readonly string[] s_changeTypes = ["Create", "Delete", "Ignore", "Deploy", "NoChange", "Modify", "Unsupported"];

    [Alias("DeploymentName")]
    [Parameter(Mandatory = false, HelpMessage = "The name of the deployment it's going to create. If not specified, defaults to the template file name when a template file is provided; defaults to the current time when a template object is provided, e.g. \"20131223140835\".")]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The deployment debug log level.")]
    [ArgumentCompletions("RequestContent", "ResponseContent", "All", "None")]
    public string? DeploymentDebugLogLevel { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The tags to put on the deployment.")]
    [ValidateNotNullOrEmpty]
    public Hashtable? Tag { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The What-If result format. Applicable when the -WhatIf or -Confirm switch is set.")]
    [ValidateSet("FullResourcePayloads", "ResourceIdOnly", IgnoreCase = true)]
    public string WhatIfResultFormat { get; set; } = "FullResourcePayloads";

    [Parameter(Mandatory = false, HelpMessage = "Comma-separated resource change types to be excluded from What-If results. Applicable when the -WhatIf or -Confirm switch is set.")]
    [ValidateChangeTypes]
    public string[]? WhatIfExcludeChangeType { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Do not ask for confirmation if there is no changes in the What-If result. Applicable when the -Confirm switch is set.")]
    public SwitchParameter ProceedIfNoChange { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Run cmdlet in the background")]
    public SwitchParameter AsJob { get; set; }

    protected override string CommandName => "New-AzTenantDeployment";

    // Legacy DeploymentCreateCmdlet.OnProcessRecord: -WhatIf/-Confirm show the what-if result through ShouldProcess.
    protected override void ProcessRecord()
    {
        Dictionary<string, object> parameters = BuildEngineParameters();
        parameters.Remove(nameof(ProceedIfNoChange));
        parameters.Remove(nameof(AsJob));
        if (Tag is not null)
        {
            parameters[nameof(Tag)] = Tag.Cast<DictionaryEntry>().ToDictionary(e => e.Key.ToString()!, e => e.Value?.ToString() ?? string.Empty);
        }

        string? whatIfMessage = null;
        string? warningMessage = null;
        string? captionMessage = null;
        bool confirm = IsConfirmRequested();
        if (IsWhatIfRequested() || confirm)
        {
            object? preview = InvokeForOutput(CommandName, new(parameters) { ["WhatIf"] = true });
            if (preview is null)
            {
                return; // the what-if failed and its error is already written; legacy stops here too
            }
            string formatted = TenantDeploymentWhatIfFormatter.Format((JsonObject)preview, cli: false, color: true);

            if (confirm && ProceedIfNoChange && !CreateTenantDeploymentProcessor_Orchestrator.HasChanges(preview as JsonObject))
            {
                WriteInformation(new HostInformationMessage { Message = formatted }, ["PSHOST"]);
                Deploy(parameters);
                return;
            }

            // Legacy: \r overrides the built-in "What if:" prefix.
            whatIfMessage = $"\r        \r{Environment.NewLine}{formatted}{Environment.NewLine}";
            warningMessage = $"{Environment.NewLine}Are you sure you want to execute the deployment?";
            captionMessage = $"\u001b[1A\u001b[0m{whatIfMessage}";
        }

        if (ShouldProcess(whatIfMessage, warningMessage, captionMessage))
        {
            Deploy(parameters);
        }
    }

    private void Deploy(Dictionary<string, object> parameters)
    {
        if (AsJob)
        {
            InvokeAsJob(CommandName, parameters);
        }
        else
        {
            Invoke(CommandName, parameters);
        }
    }

    private bool IsWhatIfRequested() =>
        MyInvocation.BoundParameters.TryGetValue("WhatIf", out object? flag)
            ? ((SwitchParameter)flag).IsPresent
            : SessionState?.PSVariable.GetValue("WhatIfPreference") switch
            {
                SwitchParameter preference => preference.IsPresent,
                bool preference => preference,
                _ => false,
            };

    private bool IsConfirmRequested() =>
        MyInvocation.BoundParameters.TryGetValue("Confirm", out object? flag)
            ? ((SwitchParameter)flag).IsPresent
            : SessionState?.PSVariable.GetValue("ConfirmPreference") is ConfirmImpact preference && ConfirmImpact.Medium >= preference;

    // Legacy ValidateChangeTypesAttribute.
    private sealed class ValidateChangeTypesAttribute : ValidateArgumentsAttribute
    {
        protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
        {
            List<string> invalid = ((IEnumerable)arguments).Cast<object>().Select(a => a.ToString()!).Distinct()
                .Where(name => !s_changeTypes.Contains(name, StringComparer.OrdinalIgnoreCase)).ToList();
            if (invalid.Count > 0)
            {
                string word = invalid.Count > 1 ? "types" : "type";
                throw new ParameterBindingException(
                    $"Unrecognized resource change {word}: {string.Join(", ", invalid)}. Specify one ore more values in the following list and try again: {string.Join(", ", s_changeTypes)}.");
            }
        }
    }
}

[Cmdlet(VerbsDiagnostic.Test, "AzTenantDeployment", DefaultParameterSetName = ParameterlessTemplateFileParameterSetName)]
[OutputType(typeof(PSResourceManagerError))]
public class TestTenantDeploymentCmdlet : TenantDeploymentTemplateCmdletBase
{
    [Alias("DeploymentName")]
    [Parameter(Mandatory = false, HelpMessage = "The name of the deployment it's going to test. If not specified, defaults to the template file name when a template file is provided")]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "When set, validation diagnostics will not be shown for valid deployments.")]
    public SwitchParameter SuppressDiagnostics { get; set; }

    protected override string CommandName => "Test-AzTenantDeployment";
}

[Cmdlet(VerbsCommon.Get, "AzTenantDeployment", DefaultParameterSetName = "GetByDeploymentName")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Processors.PSDeployment")]
public class GetTenantDeploymentCmdlet : TenantDeploymentCmdletBase
{
    [Parameter(Mandatory = false, Position = 0, ParameterSetName = "GetByDeploymentName", HelpMessage = "The name of deployment.")]
    [Alias("DeploymentName")]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "GetByDeploymentId", HelpMessage = "The fully qualified resource Id of the deployment. example: /providers/Microsoft.Resources/deployments/{deploymentName}")]
    [Alias("DeploymentId", "ResourceId")]
    [ValidateNotNullOrEmpty]
    public string? Id { get; set; }

    protected override bool EnumerateOutput => true;

    protected override void ProcessRecord()
    {
        var parameters = GetBoundParameters();
        if (parameters.Remove(nameof(Id)))
        {
            Name = GetDeploymentName(Id!);
            if (!string.IsNullOrEmpty(Name)) parameters[nameof(Name)] = Name;
        }
        Invoke(string.IsNullOrEmpty(Name) ? "Get-AzTenantDeployment_List" : "Get-AzTenantDeployment_Get", parameters);
    }
}

[Cmdlet(VerbsLifecycle.Stop, "AzTenantDeployment", SupportsShouldProcess = true, DefaultParameterSetName = "StopByDeploymentName")]
[OutputType(typeof(bool))]
public class StopTenantDeploymentCmdlet : TenantDeploymentCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "StopByDeploymentName", HelpMessage = "The name of the deployment.")]
    [Alias("DeploymentName")]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "StopByDeploymentId", HelpMessage = "The fully qualified resource Id of the deployment. example: /providers/Microsoft.Resources/deployments/{deploymentName}")]
    [Alias("DeploymentId", "ResourceId")]
    [ValidateNotNullOrEmpty]
    public string? Id { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "StopByInputObject", ValueFromPipeline = true, HelpMessage = "The deployment object.")]
    public PSDeployment? InputObject { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter PassThru { get; set; }

    protected override void ProcessRecord()
    {
        var parameters = GetBoundParameters();
        parameters.Remove(nameof(Id));
        parameters.Remove(nameof(InputObject));
        parameters.Remove(nameof(PassThru));
        parameters[nameof(Name)] = !string.IsNullOrEmpty(Name) ? Name
            : !string.IsNullOrEmpty(Id) ? GetDeploymentName(Id)
            : InputObject?.DeploymentName ?? "";

        // Legacy ConfirmAction targets -Name only, and -PassThru writes true even when declined.
        if (ShouldProcess(Name, "Cancelling active deployment ..."))
        {
            Invoke("Stop-AzTenantDeployment", parameters);
        }

        if (PassThru.IsPresent)
        {
            WriteObject(true);
        }
    }
}

[Cmdlet(VerbsCommon.Remove, "AzTenantDeployment", SupportsShouldProcess = true, DefaultParameterSetName = "RemoveByDeploymentName")]
[OutputType(typeof(bool))]
public class RemoveTenantDeploymentCmdlet : TenantDeploymentCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "RemoveByDeploymentName", HelpMessage = "The name of the deployment.")]
    [Alias("DeploymentName")]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "RemoveByDeploymentId", HelpMessage = "The fully qualified resource Id of the deployment. example: /providers/Microsoft.Resources/deployments/{deploymentName}")]
    [Alias("DeploymentId", "ResourceId")]
    [ValidateNotNullOrEmpty]
    public string? Id { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "RemoveByInputObject", ValueFromPipeline = true, HelpMessage = "The deployment object.")]
    public PSDeployment? InputObject { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Run cmdlet in the background")]
    public SwitchParameter AsJob { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter PassThru { get; set; }

    protected override void ProcessRecord()
    {
        var parameters = GetBoundParameters();
        parameters.Remove(nameof(Id));
        parameters.Remove(nameof(InputObject));
        parameters.Remove(nameof(PassThru));
        parameters.Remove(nameof(AsJob));
        parameters[nameof(Name)] = !string.IsNullOrEmpty(Name) ? Name
            : !string.IsNullOrEmpty(Id) ? GetDeploymentName(Id)
            : InputObject?.DeploymentName ?? "";

        // Legacy ConfirmAction targets -Name only; -PassThru writes true only when the delete ran.
        if (ShouldProcess(Name, "Deleting the deployment ..."))
        {
            if (AsJob)
            {
                InvokeAsJob("Remove-AzTenantDeployment", parameters, PassThru.IsPresent ? true : null);
                return;
            }
            Invoke("Remove-AzTenantDeployment", parameters);
            if (PassThru.IsPresent)
            {
                WriteObject(true);
            }
        }
    }
}

/// <summary>Legacy AzureLongRunningJob: the engine runs on a background thread and its streams are kept in the job.</summary>
internal sealed class TenantDeploymentJob : Job
{
    private readonly CancellationTokenSource _cts = new();

    public TenantDeploymentJob(string command, string name)
        : base(command, name)
    {
        PSJobTypeName = "AzureLongRunningJob";
        Streams = new OutputStreams(
            WriteOutput: (value, _) =>
            {
                value = value is SerializedJson json ? json.Value : value;
                if (value is not null)
                {
                    Output.Add(PSObject.AsPSObject(value));
                }
                return Task.CompletedTask;
            },
            WriteVerbose: (value, _) => { Verbose.Add(new VerboseRecord(value?.ToString() ?? string.Empty)); return Task.CompletedTask; },
            WriteDebug: (value, _) => { Debug.Add(new DebugRecord(value?.ToString() ?? string.Empty)); return Task.CompletedTask; },
            WriteWarning: (value, _) => { Warning.Add(new WarningRecord(value?.ToString() ?? string.Empty)); return Task.CompletedTask; },
            WriteError: (value, _) => { Error.Add(ToErrorRecord(new Exception(value?.ToString() ?? string.Empty))); return Task.CompletedTask; },
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
                Error.Add(ToErrorRecord(ex));
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

    private static ErrorRecord ToErrorRecord(Exception ex) => new(ex, "TenantDeploymentError", ErrorCategory.NotSpecified, null);
}
