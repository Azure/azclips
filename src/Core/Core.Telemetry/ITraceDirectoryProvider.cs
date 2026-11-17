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

/// <summary>
/// The per-user trace directory: the shared queue, or the local-only folder when telemetry rules
/// are bypassed.
/// </summary>
public sealed class DefaultTraceDirectoryProvider : ITraceDirectoryProvider
{
    private readonly IPersistenceStore _persistence;
    private readonly string _directoryName;

    public DefaultTraceDirectoryProvider(IPersistenceStore persistence)
        : this(persistence, TelemetryTargetResolver.BypassesRules)
    {
    }

    internal DefaultTraceDirectoryProvider(IPersistenceStore persistence, bool bypassRules)
    {
        _persistence = persistence;
        _directoryName = TelemetryTargetResolver.TraceDirectoryNameFor(bypassRules);
    }

    public string GetTraceDirectory() => _persistence.GetFilePath(_directoryName);
}
