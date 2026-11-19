// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using System.Runtime.CompilerServices;
using Core.Abstractions.Recovery;

namespace Microsoft.Azure.Azclips.Cmdlets.Accounts;

public sealed record RecoveryError(string Id, ErrorRecord Error);

/// <summary>
/// Lists AzCLIPS cmdlet errors in the current PowerShell session, newest first, with their recovery IDs.
/// </summary>
[Cmdlet(VerbsCommon.Get, "AzRecoveryId")]
[OutputType(typeof(RecoveryError))]
public class GetRecoveryIdCmdlet : PSCmdlet
{
    protected override void ProcessRecord()
    {
        foreach (var failure in PowerShellRecoveryHistory.Read(SessionState))
            WriteObject(failure);
    }
}

internal static class PowerShellRecoveryHistory
{
    private static readonly ConditionalWeakTable<ErrorRecord, RecoveryError> s_ids = new();

    internal static IEnumerable<RecoveryError> Read(SessionState sessionState)
    {
        if (sessionState.PSVariable.GetValue("global:Error") is not IList errors)
            yield break;

        var snapshot = new object[errors.Count];
        errors.CopyTo(snapshot, 0);
        foreach (var item in snapshot)
        {
            if (item is not ErrorRecord error || error.InvocationInfo?.MyCommand is not CmdletInfo cmdlet ||
                cmdlet.ImplementingType == typeof(SendRecoveryCmdlet) ||
                cmdlet.ImplementingType == typeof(GetRecoveryIdCmdlet) ||
                cmdlet.ImplementingType.Assembly.GetName().Name is not { } assemblyName ||
                !assemblyName.StartsWith("Microsoft.Azure.Azclips.Cmdlets.", StringComparison.Ordinal))
                continue;

            yield return s_ids.GetValue(error, static record =>
                new RecoveryError(FailureId.Create((uint)Environment.ProcessId), record));
        }
    }
}
