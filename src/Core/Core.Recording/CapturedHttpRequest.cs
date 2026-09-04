// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Recording;

/// <summary>
/// Immutable snapshot of an HTTP request as it left the runtime pipeline, taken at the
/// tracing-step seam (post-auth, pre-wire). Header names are sorted case-insensitively so
/// two captures can be compared or diffed deterministically. This is the request half of
/// a <see cref="SessionRecordEntry"/>; <see cref="RecordPlaybackInterceptor"/> stores the
/// response half alongside it.
/// </summary>
public sealed record CapturedHttpRequest(string Method, string Uri, IReadOnlyDictionary<string, string[]> Headers, string? Body)
{
    /// <summary>
    /// Snapshots <paramref name="request"/> as it stands right now (method, URI, all headers,
    /// buffered body). Shared by every interceptor that sits at the tracing seam.
    /// </summary>
    public static async Task<CapturedHttpRequest> SnapshotAsync(HttpRequestMessage request)
    {
        // Read the body before enumerating content headers: buffering the content is what
        // materializes Content-Length, and every capture must see the same header set.
        string? body = null;
        var headers = new SortedDictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsStringAsync();
            foreach (var (name, values) in request.Content.Headers)
            {
                headers[name] = [.. values];
            }
        }

        foreach (var (name, values) in request.Headers)
        {
            headers[name] = [.. values];
        }

        return new CapturedHttpRequest(
            request.Method.Method,
            request.RequestUri?.AbsoluteUri ?? string.Empty,
            headers,
            body);
    }

    /// <summary>
    /// Renders the capture as stable text (method, URI, sorted headers, body), used in
    /// assertion messages so a parity mismatch shows exactly which line diverged.
    /// </summary>
    public override string ToString()
    {
        var lines = new List<string> { $"{Method} {Uri}" };
        foreach (var (name, values) in Headers)
        {
            lines.Add($"{name}: {string.Join(", ", values)}");
        }
        lines.Add(string.Empty);
        lines.Add(Body ?? "<no body>");
        return string.Join(Environment.NewLine, lines);
    }
}
