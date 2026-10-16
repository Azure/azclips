// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>Resolves the directory trace files are buffered into.</summary>
/// <remarks>
/// The CLI writes each command's upload reservation here too, so the reservation is cleaned up
/// with the file it holds. <see cref="TelemetryUpload.RunPending"/> resolves its own directory.
/// </remarks>
public interface ITraceDirectoryProvider
{
    string GetTraceDirectory();
}

/// <summary>The shared per-user trace directory.</summary>
public sealed class DefaultTraceDirectoryProvider : ITraceDirectoryProvider
{
    public string GetTraceDirectory() => HomePaths.GetDefaultTraceDirectory();
}
