// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>Resolves the directory trace files are buffered into.</summary>
/// <remarks>
/// Overrides trace writes only. Reservations and uploads use the persistence root,
/// so a test or benchmark writer override cannot redirect the upload queue.
/// </remarks>
public interface ITraceDirectoryProvider
{
    string GetTraceDirectory();
}

/// <summary>The shared per-user trace directory.</summary>
public sealed class DefaultTraceDirectoryProvider(IPersistenceStore persistence) : ITraceDirectoryProvider
{
    public string GetTraceDirectory() => persistence.GetFilePath(HomePaths.TraceDirectoryName);
}
