// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// Resolves the directory that trace files are buffered into. Injected so a host can redirect
/// buffering without replacing the sender, the exporter, or any part of the export pipeline.
/// </summary>
/// <remarks>
/// This governs where traces are <b>written</b> only. The uploader side
/// (<see cref="TelemetryUpload.RunPending"/>) resolves
/// <see cref="HomePaths.GetDefaultTraceDirectory"/> directly, because it runs before the DI
/// container exists. Any production redirect through this seam must redirect the uploader too.
/// </remarks>
public interface ITraceDirectoryProvider
{
    string GetTraceDirectory();
}

/// <summary>The shipping destination: the shared per-user trace directory.</summary>
public sealed class DefaultTraceDirectoryProvider : ITraceDirectoryProvider
{
    public string GetTraceDirectory() => HomePaths.GetDefaultTraceDirectory();
}
