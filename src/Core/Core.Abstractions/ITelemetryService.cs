using System.Diagnostics;

namespace Core.Abstractions;

/// <summary>
/// Service for managing Activities
/// </summary>
public interface ITelemetryService : IDisposable
{
    /// <summary>
    /// Gets the <see cref="ActivitySource"/> used to start activities. The host's
    /// TracerProvider must <c>AddSource</c> this source's name for activities to be exported.
    /// </summary>
    ActivitySource ActivitySource { get; }

    /// <summary>
    /// Starts a new activity with the given <paramref name="name"/> and <paramref name="kind"/>.
    /// Returns <c>null</c> if no listener (TracerProvider) is attached to the underlying source.
    /// </summary>
    Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal);
}
