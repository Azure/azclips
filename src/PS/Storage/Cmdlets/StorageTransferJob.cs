// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

internal sealed class StorageTransferJob : Job
{
    private readonly CancellationTokenSource _cts = new();

    public StorageTransferJob(string name, IEngine engine, CommandInvocation invocation)
        : base(name, invocation.CommandName)
    {
        SetJobState(JobState.Running);
        _ = Task.Run(() => Run(engine, invocation));
    }

    public override string Location => "localhost";
    public override string StatusMessage => JobStateInfo.State.ToString();
    public override bool HasMoreData =>
        Output.Count > 0 || Error.Count > 0 || Warning.Count > 0
        || Verbose.Count > 0 || Debug.Count > 0 || Information.Count > 0;

    public override void StopJob()
    {
        _cts.Cancel();
        SetJobState(JobState.Stopped);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Cancel();
            _cts.Dispose();
        }
        base.Dispose(disposing);
    }

    private void Run(IEngine engine, CommandInvocation invocation)
    {
        var streams = new OutputStreams(
            (value, _) =>
            {
                Output.Add(PSObject.AsPSObject(value));
                return Task.CompletedTask;
            },
            (value, _) =>
            {
                Verbose.Add(new VerboseRecord(value?.ToString() ?? string.Empty));
                return Task.CompletedTask;
            },
            (value, _) =>
            {
                Debug.Add(new DebugRecord(value?.ToString() ?? string.Empty));
                return Task.CompletedTask;
            },
            (value, _) =>
            {
                Warning.Add(new WarningRecord(value?.ToString() ?? string.Empty));
                return Task.CompletedTask;
            },
            (value, _) =>
            {
                Error.Add(new ErrorRecord(
                    new Exception(value?.ToString() ?? string.Empty),
                    "StorageTransferError",
                    ErrorCategory.NotSpecified,
                    null));
                return Task.CompletedTask;
            },
            (value, _) =>
            {
                Information.Add(new InformationRecord(value, invocation.CommandName));
                return Task.CompletedTask;
            });

        try
        {
            using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
            session.Run();
            if (JobStateInfo.State != JobState.Stopped)
            {
                SetJobState(JobState.Completed);
            }
        }
        catch (OperationCanceledException)
        {
            SetJobState(JobState.Stopped);
        }
        catch (Exception exception)
        {
            Error.Add(new ErrorRecord(exception, "StorageTransferError", ErrorCategory.NotSpecified, null));
            SetJobState(JobState.Failed);
        }
    }
}
