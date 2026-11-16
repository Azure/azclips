// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;

namespace Tests.Common.Utilities;

/// <summary>
/// Hang guards for Agent and recovery tests: limits on awaits for events that must happen on real
/// threads, processes or I/O. A guard only stops a hung test; it never decides the outcome of a
/// test that makes progress.
/// </summary>
public static class AgentTestTimeouts
{
    /// <summary>The limit for an event that must happen.</summary>
    public static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(60);

    public static Task WithAgentHangGuard(this Task task,
        [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0) =>
        GuardAsync(task, filePath, lineNumber);

    public static Task<T> WithAgentHangGuard<T>(this Task<T> task,
        [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0) =>
        GuardAsync(task, filePath, lineNumber);

    public static Task WithAgentHangGuard(this ValueTask task,
        [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0) =>
        GuardAsync(task.AsTask(), filePath, lineNumber);

    private static async Task GuardAsync(Task task, string? filePath, int lineNumber)
    {
        try
        {
            await task.WaitAsync(HangGuard);
        }
        catch (TimeoutException) when (!task.IsCompleted)
        {
            throw new TimeoutException(CreateMessage(filePath, lineNumber));
        }
    }

    private static async Task<T> GuardAsync<T>(Task<T> task, string? filePath, int lineNumber)
    {
        try
        {
            return await task.WaitAsync(HangGuard);
        }
        catch (TimeoutException) when (!task.IsCompleted)
        {
            throw new TimeoutException(CreateMessage(filePath, lineNumber));
        }
    }

    private static string CreateMessage(string? filePath, int lineNumber) =>
        string.IsNullOrEmpty(filePath)
            ? $"The operation timed out after {HangGuard}."
            : $"The operation at {filePath}:{lineNumber} timed out after {HangGuard}.";
}
