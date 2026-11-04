// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Tests.Recording;

/// <summary>
/// Decides whether an outgoing request corresponds to a recorded one during playback.
/// Pluggable so scenarios whose requests carry non-deterministic fields (client-generated
/// GUIDs, timestamps) can normalize before comparing instead of failing to match.
/// </summary>
public interface IRequestMatcher
{
    /// <summary>Short description of the strategy, quoted in unmatched-playback diagnostics.</summary>
    string Description { get; }

    bool Matches(RecordedRequest recorded, CapturedHttpRequest outgoing);
}

/// <summary>
/// The default strategy: HTTP method + absolute URI, compared case-insensitively (host names
/// are case-insensitive and ARM tolerates path casing), optionally plus the exact text or binary body.
/// Headers deliberately never participate: <c>Authorization</c> is stripped from recordings,
/// and volatile headers such as <c>User-Agent</c> would make matching brittle for no gain.
/// </summary>
public sealed class DefaultRequestMatcher(bool matchBody = false) : IRequestMatcher
{
    public string Description => matchBody ? "method + URI + body" : "method + URI";

    public bool Matches(RecordedRequest recorded, CapturedHttpRequest outgoing)
        => string.Equals(recorded.Method, outgoing.Method, StringComparison.OrdinalIgnoreCase)
        && string.Equals(recorded.Uri, outgoing.Uri, StringComparison.OrdinalIgnoreCase)
        && (!matchBody || BodiesMatch(recorded, outgoing));

    private static bool BodiesMatch(RecordedRequest recorded, CapturedHttpRequest outgoing)
        => recorded.BodyBase64 is { } binary
            ? outgoing.BinaryBody is not null &&
                Convert.FromBase64String(binary).AsSpan().SequenceEqual(outgoing.BinaryBody)
            : outgoing.BinaryBody is null && string.Equals(recorded.Body, outgoing.Body, StringComparison.Ordinal);
}
