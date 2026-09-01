namespace Core.Abstractions;

/// <summary>
/// Fetches the ARM data boundary for the current tenant. The data boundary indicates
/// which regional boundary (e.g. "EU" or "Global") governs data residency and is used
/// to route telemetry to the correct Aria ingestion endpoint.
/// </summary>
public interface IDataBoundaryService
{
    /// <summary>
    /// Queries the ARM dataBoundaries API and returns the boundary value (e.g. "EU", "Global"),
    /// or <see langword="null"/> when the API is unavailable or returns no value.
    /// </summary>
    Task<string?> GetDataBoundaryAsync(string accessToken, CancellationToken cancellationToken = default);
}
