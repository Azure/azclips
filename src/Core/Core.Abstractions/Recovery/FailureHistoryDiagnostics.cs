// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Recovery;

/// <summary>
/// Writes the full text of failure-history errors at debug level.
/// </summary>
public static class FailureHistoryDiagnostics
{
    /// <summary>
    /// Writes the full text of the exception, with the message and stack trace of each inner exception,
    /// at debug level when the chain wraps a failure-history error.
    /// </summary>
    public static void EmitCauseChain(Exception ex, ILoggingService? logger)
    {
        if (ex.InnerException is null || !HasFailureHistoryError(ex))
            return;
        logger?.Debug($"error cause: {ex}");
    }

    private static bool HasFailureHistoryError(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
            if (current is FailureHistoryException)
                return true;
        return false;
    }
}
