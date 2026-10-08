// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

// Backs -AsJob on New-/Remove-AzTenantDeployment. PowerShell's own Job infrastructure
// (Get-Job/Receive-Job/Wait-Job/Stop-Job) is thread-safe by design, so engine output is written
// directly into the job's data collections from a background task instead of through a
// PSCmdlet, which can only be touched from the pipeline thread.
internal sealed class EngineCommandJob : Job
{
    private readonly CancellationTokenSource _cts = new();

    public EngineCommandJob(string command, string? target)
        : base(command, target)
    {
    }

    public CancellationToken CancellationToken => _cts.Token;

    public override string Location => "localhost";

    public override string StatusMessage => JobStateInfo.State.ToString();

    public override bool HasMoreData =>
        Output.Count > 0 || Error.Count > 0 || Warning.Count > 0 || Verbose.Count > 0 || Debug.Count > 0;

    public override void StopJob()
    {
        _cts.Cancel();
    }

    public void Start(Func<Task> work)
    {
        SetJobState(JobState.Running);
        _ = RunAsync(work);
    }

    private async Task RunAsync(Func<Task> work)
    {
        try
        {
            await work();
            SetJobState(_cts.IsCancellationRequested ? JobState.Stopped : JobState.Completed);
        }
        catch (OperationCanceledException)
        {
            SetJobState(JobState.Stopped);
        }
        catch (Exception ex)
        {
            Error.Add(new ErrorRecord(ex, "EngineCommandJobError", ErrorCategory.NotSpecified, null));
            SetJobState(JobState.Failed);
        }
    }
}

internal static class JobOutputStreamsFactory
{
    public static OutputStreams Create(Job job, string errorId) => new(
        WriteOutput: (value, _) =>
        {
            if (value is SerializedJson serializedJson)
            {
                value = serializedJson.Value;
            }

            job.Output.Add(value is null ? new PSObject() : PSObject.AsPSObject(value));
            return Task.CompletedTask;
        },
        WriteVerbose: (value, _) =>
        {
            job.Verbose.Add(new VerboseRecord(value?.ToString() ?? string.Empty));
            return Task.CompletedTask;
        },
        WriteDebug: (value, _) =>
        {
            job.Debug.Add(new DebugRecord(value?.ToString() ?? string.Empty));
            return Task.CompletedTask;
        },
        WriteWarning: (value, _) =>
        {
            job.Warning.Add(new WarningRecord(value?.ToString() ?? string.Empty));
            return Task.CompletedTask;
        },
        WriteError: (value, _) =>
        {
            job.Error.Add(new ErrorRecord(new Exception(value?.ToString() ?? string.Empty), errorId, ErrorCategory.NotSpecified, null));
            return Task.CompletedTask;
        },
        WriteInformation: (_, _) => Task.CompletedTask);
}
