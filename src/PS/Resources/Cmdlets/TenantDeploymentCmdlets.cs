// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
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

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "TenantDeploymentError", ErrorCategory.NotSpecified, enumerateOutput: EnumerateOutput);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected virtual Dictionary<string, object> GetBoundParameters() =>
        MyInvocation.BoundParameters
            .Where(p => !CommonParameters.Contains(p.Key) && !OptionalCommonParameters.Contains(p.Key))
            .ToDictionary(p => p.Key, p => p.Value is SwitchParameter s ? s.IsPresent : p.Value);

    protected void Invoke(string commandName, Dictionary<string, object> parameters) => Invoke(commandName, parameters, streams, _cts.Token);

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

    private void Invoke(string commandName, Dictionary<string, object> parameters, OutputStreams outputStreams, CancellationToken token)
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var invocation = new CommandInvocation("Resources", commandName, parameters, false, MyInvocation.MyCommand.ModuleName, MyInvocation.MyCommand.Version?.ToString());
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, outputStreams, token);
        session.Run();
    }

    // Runs a command and returns its last output object instead of writing it to the pipeline.
    protected object? InvokeForOutput(string commandName, Dictionary<string, object> parameters)
    {
        OutputStreams original = streams;
        object? output = null;
        streams = original with { WriteOutput = (value, _) => { output = value; return Task.CompletedTask; } };
        try
        {
            Invoke(commandName, parameters);
        }
        finally
        {
            streams = original;
        }
        return output;
    }

    protected override void EndProcessing() => _cts.Dispose();

    // Stop/Remove: a piped deployment binds as its Id, else its DeploymentName, like legacy InputObject.DeploymentName;
    // one with neither writes a non-terminating error and returns null so the record is skipped.
    protected string? ResolveTarget(PSDeployment? inputObject, string? name, string? id, Dictionary<string, object> parameters)
    {
        parameters.Remove("InputObject");
        if (inputObject is null)
        {
            return name ?? id;
        }
        if (!string.IsNullOrEmpty(inputObject.Id))
        {
            parameters["Id"] = inputObject.Id;
            return inputObject.Id;
        }
        if (!string.IsNullOrEmpty(inputObject.DeploymentName))
        {
            parameters["Name"] = inputObject.DeploymentName;
            return inputObject.DeploymentName;
        }
        WriteError(new ErrorRecord(new ArgumentException("The deployment object has neither an Id nor a DeploymentName."), "TenantDeploymentInputObjectInvalid", ErrorCategory.InvalidArgument, inputObject));
        return null;
    }

    protected override void StopProcessing() => _cts.Cancel();
}

/// <summary>New-/Test-AzTenantDeployment inputs, the same as New-/Test-AzResourceGroupDeployment plus Location.</summary>
public abstract class TenantDeploymentTemplateCmdletBase : TenantDeploymentCmdletBase, IDynamicParameters
{
    private RuntimeDefinedParameterDictionary dynamicParameters = new();

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? TemplateUri { get; set; }

    [Parameter(Mandatory = false)]
    public string? TemplateFile { get; set; }

    [Parameter(Mandatory = false)]
    public string? TemplateSpecId { get; set; }

    [Parameter(Mandatory = false)]
    public string? QueryString { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? Parameter { get; set; }

    [Parameter(Mandatory = false)]
    public string? ValidationLevel { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter SkipTemplateParameterPrompt { get; set; }

    // Like New-AzTenantDeployment, each parameter of a local template becomes a cmdlet parameter, and PowerShell prompts
    // for required ones that have no value. Runs on tab completion too, so a template that cannot be read adds nothing.
    public object GetDynamicParameters()
    {
        dynamicParameters = new RuntimeDefinedParameterDictionary();
        if (SkipTemplateParameterPrompt.IsPresent)
        {
            return dynamicParameters;
        }
        try
        {
            dynamicParameters = TenantDeploymentTemplateParameters.Create(
                GetType(),
                ResolvePath(TemplateFile),
                GetBound("TemplateObject") as Hashtable,
                TenantDeploymentTemplateParameters.GetSuppliedNames(ResolvePath(GetBound("ParametersFile") as string), GetBound("ParametersUri") as string, Parameter));
        }
        catch (Exception)
        {
        }
        return dynamicParameters;
    }

    // Bound template parameters reach the extension as one -Parameter JSON object that overrides other parameter values.
    protected override Dictionary<string, object> GetBoundParameters()
    {
        Dictionary<string, object> parameters = base.GetBoundParameters();
        parameters.Remove(nameof(SkipTemplateParameterPrompt));
        List<KeyValuePair<string, object?>> templateValues = [];
        foreach (RuntimeDefinedParameter parameter in dynamicParameters.Values.Where(p => p.IsSet))
        {
            parameters.Remove(parameter.Name);
            templateValues.Add(new(((ParameterAttribute)parameter.Attributes[0]).HelpMessage, parameter.Value));
        }
        if (templateValues.Count > 0)
        {
            parameters[nameof(Parameter)] = (Parameter ?? []).Append(TenantDeploymentTemplateParameters.ToParameterToken(templateValues)).ToArray();
        }
        return parameters;
    }

    private object? GetBound(string name) => MyInvocation.BoundParameters.TryGetValue(name, out object? value) ? value is PSObject ps ? ps.BaseObject : value : null;

    private string? ResolvePath(string? path) => string.IsNullOrWhiteSpace(path) ? path : SessionState.Path.GetUnresolvedProviderPathFromPSPath(path);
}

[Cmdlet(VerbsCommon.New, "AzTenantDeployment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(PSDeployment))]
public class NewTenantDeploymentCmdlet : TenantDeploymentTemplateCmdletBase
{
    [Alias("DeploymentName")]
    [Parameter(Mandatory = false)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false)]
    public Hashtable? TemplateObject { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("TemplateParameterUri")]
    public string? ParametersUri { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("TemplateParameterFile")]
    public string? ParametersFile { get; set; }

    [Parameter(Mandatory = false)]
    public Hashtable? Tag { get; set; }

    [Parameter(Mandatory = false)]
    public string? DeploymentDebugLogLevel { get; set; }

    [Parameter(Mandatory = false)]
    public string? WhatIfResultFormat { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? WhatIfExcludeChangeType { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter ProceedIfNoChange { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Run cmdlet in the background")]
    public SwitchParameter AsJob { get; set; }

    // Like New-AzTenantDeployment, -WhatIf and -Confirm show the what-if change report before (or instead of) deploying.
    // With -AsJob only the deployment runs in the background; the what-if report and confirmation stay in the foreground.
    protected override void ProcessRecord()
    {
        Dictionary<string, object> parameters = GetBoundParameters();
        parameters.Remove(nameof(AsJob));
        if (Tag is not null)
        {
            parameters["Tag"] = Tag.Cast<DictionaryEntry>().ToDictionary(e => e.Key.ToString()!, e => e.Value?.ToString() ?? "");
        }

        bool confirm = IsConfirmRequested();
        if (confirm || IsWhatIfRequested())
        {
            Dictionary<string, object> whatIfParameters = new(parameters) { ["NoPrettyPrint"] = true };
            whatIfParameters.Remove(nameof(Tag));
            whatIfParameters.Remove(nameof(DeploymentDebugLogLevel));
            whatIfParameters.Remove(nameof(ProceedIfNoChange));
            if (InvokeForOutput("New-AzTenantDeployment_WhatIf", whatIfParameters) is not WhatIfOperationResult result)
            {
                return;
            }

            string report = WhatIfReportFormatter.Format(result);
            bool noChange = (result.Properties?.Changes ?? []).All(c =>
                string.Equals(c.ChangeType, "NoChange", StringComparison.OrdinalIgnoreCase) || string.Equals(c.ChangeType, "Ignore", StringComparison.OrdinalIgnoreCase));
            if (confirm && ProceedIfNoChange.IsPresent && noChange)
            {
                WriteInformation(new HostInformationMessage { Message = report }, ["PSHOST"]);
            }
            else if (!ShouldProcess(Environment.NewLine + report, "Are you sure you want to execute the deployment?", report))
            {
                return;
            }
        }
        else if (!ShouldProcess(string.IsNullOrEmpty(Name) ? "deployment at tenant scope" : $"deployment '{Name}' at tenant scope", "Create"))
        {
            return;
        }

        parameters.Remove(nameof(WhatIfResultFormat));
        parameters.Remove(nameof(WhatIfExcludeChangeType));
        parameters.Remove(nameof(ProceedIfNoChange));
        if (AsJob.IsPresent)
        {
            InvokeAsJob("New-AzTenantDeployment", parameters);
            return;
        }
        Invoke("New-AzTenantDeployment", parameters);
    }

    private bool IsWhatIfRequested() =>
        MyInvocation.BoundParameters.TryGetValue("WhatIf", out object? value)
            ? ((SwitchParameter)value).IsPresent
            : LanguagePrimitives.IsTrue(SessionState?.PSVariable.GetValue("WhatIfPreference"));

    private bool IsConfirmRequested() =>
        MyInvocation.BoundParameters.TryGetValue("Confirm", out object? value)
            ? ((SwitchParameter)value).IsPresent
            : SessionState?.PSVariable.GetValue("ConfirmPreference") is ConfirmImpact preference && ConfirmImpact.Medium >= preference;
}

// Validate is read-only, so like Test-AzResourceGroupDeployment it does not support ShouldProcess.
// Like legacy Test-AzTenantDeployment: writes the validation error object, or nothing (plus a diagnostics warning) when valid.
[Cmdlet(VerbsDiagnostic.Test, "AzTenantDeployment")]
[OutputType(typeof(ErrorResponse))]
public class TestTenantDeploymentCmdlet : TenantDeploymentTemplateCmdletBase
{
    [Alias("DeploymentName")]
    [Parameter(Mandatory = false)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false)]
    public Hashtable? TemplateObject { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("TemplateParameterUri")]
    public string? ParametersUri { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("TemplateParameterFile")]
    public string? ParametersFile { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter SuppressDiagnostics { get; set; }

    protected override void ProcessRecord()
    {
        Dictionary<string, object> parameters = GetBoundParameters();
        parameters["PowerShellOutput"] = true;
        Invoke("Test-AzTenantDeployment", parameters);
    }
}

// What-if is a read-only preview, so like New-AzResourceGroupDeploymentWhatIf it does not support ShouldProcess.
[Cmdlet(VerbsCommon.Get, "AzTenantDeploymentWhatIfResult")]
[OutputType(typeof(WhatIfOperationResult))]
public class GetTenantDeploymentWhatIfResultCmdlet : TenantDeploymentTemplateCmdletBase
{
    [Parameter(Mandatory = false)]
    [Alias("DeploymentName")]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false)]
    public Hashtable? TemplateObject { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("TemplateParameterUri")]
    public string? ParametersUri { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("TemplateParameterFile")]
    public string? ParametersFile { get; set; }

    [Parameter(Mandatory = false)]
    public string? ResultFormat { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? ExcludeChangeType { get; set; }

    protected override void ProcessRecord()
    {
        Dictionary<string, object> parameters = GetBoundParameters();
        parameters["NoPrettyPrint"] = true;
        Invoke("Get-AzTenantDeploymentWhatIfResult", parameters);
    }
}

[Cmdlet(VerbsData.Save, "AzTenantDeploymentTemplate", SupportsShouldProcess = true, DefaultParameterSetName = "SaveByDeploymentName")]
[OutputType(typeof(DeploymentTemplateSaveResult))]
public class SaveTenantDeploymentTemplateCmdlet : TenantDeploymentCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "SaveByDeploymentName", HelpMessage = "The name of the deployment.")]
    [Alias("DeploymentName")]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "SaveByDeploymentObject", ValueFromPipeline = true, HelpMessage = "The deployment object.")]
    public PSDeployment? DeploymentObject { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The output path of the template file.")]
    [ValidateNotNullOrEmpty]
    public string? Path { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Overwrite the destination file when it already exists.")]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {
        string? name = DeploymentObject is null ? Name : DeploymentObject.DeploymentName;
        if (string.IsNullOrEmpty(name))
        {
            WriteError(new ErrorRecord(new ArgumentException("The deployment object has no DeploymentName."), "TenantDeploymentInputObjectInvalid", ErrorCategory.InvalidArgument, DeploymentObject));
            return;
        }
        if (!ShouldProcess(name, VerbsData.Save))
        {
            return;
        }

        Dictionary<string, object> parameters = GetBoundParameters();
        parameters.Remove(nameof(DeploymentObject));
        parameters["Name"] = name;
        // A relative path is relative to the PowerShell location, not the process working directory.
        parameters["Path"] = Path is null ? SessionState.Path.CurrentFileSystemLocation.Path : GetUnresolvedProviderPathFromPSPath(Path);
        Invoke("Save-AzTenantDeploymentTemplate", parameters);
    }
}
[Cmdlet(VerbsCommon.Get, "AzTenantDeployment", DefaultParameterSetName = "GetByDeploymentName")]
[OutputType(typeof(PSDeployment))]
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
        Invoke(Id is not null ? "Get-AzTenantDeployment_GetById"
            : string.IsNullOrEmpty(Name) ? "Get-AzTenantDeployment_List"
            : "Get-AzTenantDeployment_Get", GetBoundParameters());
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
        parameters.Remove(nameof(PassThru));
        if (ResolveTarget(InputObject, Name, Id, parameters) is not { } target)
        {
            return;
        }

        // -PassThru writes true only when the cancel ran.
        if (ShouldProcess(target, "Cancelling active deployment ..."))
        {
            Invoke("Stop-AzTenantDeployment", parameters);
            if (PassThru.IsPresent)
            {
                WriteObject(true);
            }
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
        parameters.Remove(nameof(PassThru));
        parameters.Remove(nameof(AsJob));
        if (ResolveTarget(InputObject, Name, Id, parameters) is not { } target)
        {
            return;
        }

        // -PassThru writes true only when the delete ran; with -AsJob the job outputs it after the delete.
        if (ShouldProcess(target, "Deleting the deployment ..."))
        {
            if (AsJob.IsPresent)
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
