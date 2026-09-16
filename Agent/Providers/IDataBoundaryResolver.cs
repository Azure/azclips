namespace Core.Agent.Providers;

/// <summary>Resolves the active profile tenant's ARM data boundary from a token the caller holds.</summary>
/// <remarks>
/// Extracted so the recovery startup seam can be driven with a fake.
/// <see cref="DataBoundaryResolver"/> is <c>sealed</c>, so a factory typed to the concrete class
/// could only ever hand back the real thing and the seam's tests would be asserting against a
/// live ARM call rather than against the seam.
///
/// <para>Disposable because an implementation may own the <see cref="HttpClient"/> it created.</para>
/// </remarks>
internal interface IDataBoundaryResolver : IDisposable
{
    /// <summary>
    /// Resolves the boundary, classifying every failure into the returned value rather than
    /// throwing. Only a cancellation raised by <paramref name="ct"/> propagates.
    /// </summary>
    Task<DataBoundaryResult> ResolveAsync(string accessToken, CancellationToken ct = default);
}
