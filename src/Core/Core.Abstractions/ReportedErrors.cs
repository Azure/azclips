// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Marks an exception whose message was already written to the error stream, so the host does not print it again.
/// </summary>
public static class ReportedErrors
{
    private const string DataKey = "Azclips.ErrorReported";

    public static T MarkReported<T>(T exception) where T : Exception
    {
        exception.Data[DataKey] = true;
        return exception;
    }

    public static bool IsReported(Exception exception) => exception.Data.Contains(DataKey);
}
