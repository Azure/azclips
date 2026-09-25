// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using Core.Abstractions;
using Core.Abstractions.Recovery;

namespace Core.Cli.Recovery;

internal static class TroubleshootSelection
{
    internal static FailureRecord Select(
        IReadOnlyList<FailureRecord> records,
        string? id,
        Func<uint> getParentProcessId,
        Action<string> reportWarning)
    {
        if (id is not null)
        {
            return records.FirstOrDefault(record => record.Id.Equals(id, StringComparison.Ordinal))
                ?? throw new AzclipsErrorException("FailureRecordNotFound", 1,
                    "The selected failure record was not found or has been evicted. Use az troubleshoot --list to select a saved failure.");
        }

        var latest = records.MaxBy(record => record.Sequence)
            ?? throw new AzclipsErrorException("NoFailureHistory", 1,
                "No saved failures are available. Set agent.recovery to manual or auto, then run the failing command again.");
        var parentProcessId = TryGetParentProcessId(getParentProcessId, reportWarning);
        return records.Where(record => record.TerminalSessionId == parentProcessId).MaxBy(record => record.Sequence)
            ?? latest;
    }

    private static uint? TryGetParentProcessId(Func<uint> getParentProcessId, Action<string> reportWarning)
    {
        try
        {
            var processId = getParentProcessId();
            if (processId == 0)
                throw new InvalidOperationException("The parent process ID must be positive.");
            return processId;
        }
        catch (Exception ex) when (ex is Win32Exception or PlatformNotSupportedException or
            DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or OverflowException)
        {
            reportWarning("Unable to resolve the current terminal session. Using the latest saved failure across all terminals.");
            return null;
        }
    }
}
