// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Recovery;

/// <summary>
/// Writes the cause chain of failure-history errors at debug level.
/// </summary>
public static class FailureHistoryDiagnostics
{
    /// <summary>
    /// Writes one debug line with the type, message, and HResult of each exception in the chain
    /// when the chain wraps a failure-history error. Stack traces are never written.
    /// </summary>
    public static void EmitCauseChain(Exception ex, ILoggingService? logger)
    {
        if (ex.InnerException is null || !HasFailureHistoryError(ex))
            return;
        logger?.Debug($"error cause: {DescribeCauseChain(ex)}");
    }

    public static string DescribeCauseChain(Exception ex)
    {
        var levels = new List<string>();
        for (Exception? current = ex; current is not null; current = current.InnerException)
            levels.Add($"{current.GetType().FullName}: {current.Message} (HResult 0x{current.HResult:X8})");
        return string.Join(" ---> ", levels);
    }

    private static bool HasFailureHistoryError(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
            if (current is FailureHistoryException)
                return true;
        return false;
    }
}
