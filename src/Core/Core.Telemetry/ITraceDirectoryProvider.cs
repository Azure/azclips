// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>Resolves the directory trace files are buffered into.</summary>
/// <remarks>
/// Applies to writes only — <see cref="TelemetryUpload.RunPending"/> resolves its own directory.
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
