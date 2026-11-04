// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>Resolves the directory trace files are buffered into.</summary>
/// <remarks>
/// Shared by trace writers, upload reservations, and the uploader.
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
