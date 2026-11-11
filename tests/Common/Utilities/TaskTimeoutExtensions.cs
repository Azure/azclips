// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;

namespace Tests.Common.Utilities;

/// <summary>
/// Hang guards for awaits on real threads, processes or I/O. A guard only stops a hung test;
/// it never decides the outcome of a test that makes progress.
/// </summary>
public static class TaskTimeoutExtensions
{
    /// <summary>The default limit for an event that must happen.</summary>
    public static readonly TimeSpan DefaultTimeoutTimeSpan = TimeSpan.FromSeconds(60);

    public static Task DefaultTimeout(this Task task,
        [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0) =>
        task.TimeoutAfter(DefaultTimeoutTimeSpan, filePath, lineNumber);

    public static Task<T> DefaultTimeout<T>(this Task<T> task,
        [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0) =>
        task.TimeoutAfter(DefaultTimeoutTimeSpan, filePath, lineNumber);

    public static Task DefaultTimeout(this ValueTask task,
        [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0) =>
        task.AsTask().TimeoutAfter(DefaultTimeoutTimeSpan, filePath, lineNumber);

    public static async Task TimeoutAfter(this Task task, TimeSpan timeout,
        [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            await task.WaitAsync(timeout);
        }
        catch (TimeoutException) when (!task.IsCompleted)
        {
            throw new TimeoutException(CreateMessage(timeout, filePath, lineNumber));
        }
    }

    public static async Task<T> TimeoutAfter<T>(this Task<T> task, TimeSpan timeout,
        [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            return await task.WaitAsync(timeout);
        }
        catch (TimeoutException) when (!task.IsCompleted)
        {
            throw new TimeoutException(CreateMessage(timeout, filePath, lineNumber));
        }
    }

    private static string CreateMessage(TimeSpan timeout, string? filePath, int lineNumber) =>
        string.IsNullOrEmpty(filePath)
            ? $"The operation timed out after {timeout}."
            : $"The operation at {filePath}:{lineNumber} timed out after {timeout}.";
}
