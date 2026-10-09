namespace Core.Agent.Providers;

/// <summary>Queries the active tenant's ARM boundary using the caller's token.</summary>
/// <remarks>Implementations dispose clients they own, not clients they borrow.</remarks>
internal interface IDataBoundaryResolver : IDisposable
{
    /// <summary>
    /// Resolves the boundary, classifying every failure into the returned value rather than
    /// throwing. Only a cancellation raised by <paramref name="ct"/> propagates.
    /// </summary>
    Task<DataBoundaryResult> ResolveAsync(string accessToken, CancellationToken ct = default);
}
