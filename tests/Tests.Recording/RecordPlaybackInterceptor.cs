// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Tests.Recording;

public enum RecorderMode
{
    Record,
    Playback,
}

/// <summary>
/// The record/playback <c>SendAsyncStep</c>, registered as the <see cref="ITracingInterceptor"/>
/// singleton to fill the seam <c>Engine.CreateProcessorContext</c> reserves at the tracing step.
/// <para>
/// Record forwards to the wire, then stores the request/response pair, sanitised so no live token,
/// subscription, or tenant id reaches disk. Playback matches the request via <see cref="IRequestMatcher"/>
/// (default: method + URI, each entry used once, duplicates in recorded order) and returns the stored
/// response without hitting the network; an unmatched request throws, listing the unconsumed entries.
/// </para>
/// </summary>
public sealed class RecordPlaybackInterceptor(
    RecorderMode mode,
    SessionRecord sessionRecord,
    IRequestMatcher? matcher = null,
    RecordingSanitizers? sanitizers = null,
    IEnumerable<int>? alreadyConsumed = null,
    Action<int>? onConsumed = null,
    Action<SessionRecordEntry>? onRecorded = null) : ITracingInterceptor, IHttpTerminateHandler
{
    private readonly IRequestMatcher _matcher = matcher ?? new DefaultRequestMatcher();
    private readonly RecordingSanitizers _sanitizers = sanitizers ?? RecordingSanitizers.Default;
    private readonly List<CapturedHttpRequest> _sent = [];

    // Scripted scenarios run one process per command; the seed and callbacks let consumption
    // and captured entries persist across those processes (see RecordingHook).
    private readonly HashSet<int> _consumed = [.. alreadyConsumed ?? []];

    // Orchestrators fan out requests concurrently (e.g. vm create fires vnet + nsg in parallel),
    // so every touch of the shared lists goes through this gate.
    private readonly object _gate = new();

    public RecorderMode Mode { get; } = mode;

    public SessionRecord SessionRecord { get; } = sessionRecord;

    /// <summary>Every request that reached this step, in send order (kept in memory only, unsanitized).</summary>
    public IReadOnlyList<CapturedHttpRequest> Sent
    {
        get
        {
            lock (_gate)
            {
                return [.. _sent];
            }
        }
    }

    public SendAsyncStep SendAsync => async (HttpRequestMessage request, IEventPublisher callback, ISendAsync next) =>
    {
        var snapshot = await CapturedHttpRequest.SnapshotAsync(request);
        lock (_gate)
        {
            _sent.Add(snapshot);
        }
        return Mode == RecorderMode.Record
            ? await RecordAsync(snapshot, request, callback, next)
            : Playback(snapshot, request.HasSensitiveDiagnostics());
    };

    private async Task<HttpResponseMessage> RecordAsync(CapturedHttpRequest snapshot, HttpRequestMessage request, IEventPublisher callback, ISendAsync next)
    {
        var response = await next.SendAsync(request, callback);

        string? responseBody = null;
        var contentHeaders = new Dictionary<string, string[]>();
        if (response.Content is not null)
        {
            responseBody = await response.Content.ReadAsStringAsync();
            foreach (var (name, values) in response.Content.Headers)
            {
                contentHeaders[name] = [.. values];
            }

            // Re-wrap the body so downstream consumers (deserialization, LRO peeks) read it
            // as if the recorder wasn't there.
            var replacement = new StringContent(responseBody);
            replacement.Headers.Clear();
            foreach (var (name, values) in contentHeaders)
            {
                replacement.Headers.TryAddWithoutValidation(name, values);
            }
            response.Content = replacement;
        }

        var responseHeaders = new Dictionary<string, string[]>();
        foreach (var (name, values) in response.Headers)
        {
            responseHeaders[name] = [.. values];
        }

        bool sensitive = request.HasSensitiveDiagnostics();
        var bodySanitizer = new SensitiveJsonSanitizer();
        var entry = new SessionRecordEntry
        {
            Request = _sanitizers.Sanitize(new RecordedRequest
            {
                Method = snapshot.Method,
                Uri = snapshot.Uri,
                Headers = sensitive ? [] : WithoutDerivedHeaders(snapshot.Headers, snapshot.Body),
                Body = sensitive && snapshot.Body is not null ? bodySanitizer.Sanitize(snapshot.Body) : snapshot.Body,
            }),
            Response = _sanitizers.Sanitize(new RecordedResponse
            {
                StatusCode = (int)response.StatusCode,
                Headers = sensitive ? [] : responseHeaders,
                ContentHeaders = sensitive ? [] : WithoutDerivedHeaders(contentHeaders, responseBody),
                Body = sensitive && responseBody is not null ? bodySanitizer.Sanitize(responseBody) : responseBody,
            }),
        };

        lock (_gate)
        {
            SessionRecord.Entries.Add(entry);
            onRecorded?.Invoke(entry);
        }

        return response;
    }

    // With a body, Content-Length is derived from it and sanitising may rewrite the body, so drop it
    // and let playback recompute it from the stored body. Without one (e.g. a HEAD response) it
    // describes the resource, not the body, so it is kept.
    private static Dictionary<string, string[]> WithoutDerivedHeaders(IReadOnlyDictionary<string, string[]> headers, string? body)
        => headers
            .Where(header => string.IsNullOrEmpty(body) || !string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(header => header.Key, header => header.Value);

    private HttpResponseMessage Playback(CapturedHttpRequest snapshot, bool sensitive)
    {
        if (sensitive && snapshot.Body is not null)
            snapshot = snapshot with { Body = new SensitiveJsonSanitizer().Sanitize(snapshot.Body) };
        // Normalise the request the same way the recording was sanitised, so a live id still matches.
        snapshot = _sanitizers.Normalize(snapshot);

        // Match and consume atomically, so concurrent requests can't claim the same entry.
        lock (_gate)
        {
            for (int i = 0; i < SessionRecord.Entries.Count; i++)
            {
                var entry = SessionRecord.Entries[i];
                // Sanitise the recorded side too. Sanitising is idempotent, so this is a no-op for
                // entries recorded under the current rule set, and it keeps recordings captured
                // before a rule was added replayable instead of forcing a re-record.
                if (!_consumed.Contains(i) && _matcher.Matches(_sanitizers.Sanitize(entry.Request), snapshot))
                {
                    _consumed.Add(i);
                    onConsumed?.Invoke(i);
                    return ToResponse(entry.Response);
                }
            }

            throw new InvalidOperationException(BuildUnmatchedDiagnostic(snapshot));
        }
    }

    private static HttpResponseMessage ToResponse(RecordedResponse recorded)
    {
        var response = new HttpResponseMessage((HttpStatusCode)recorded.StatusCode);

        if (recorded.Body is not null || recorded.ContentHeaders.Count > 0)
        {
            var content = new StringContent(recorded.Body ?? string.Empty, Encoding.UTF8);
            content.Headers.Clear();
            foreach (var (name, values) in recorded.ContentHeaders)
            {
                content.Headers.TryAddWithoutValidation(name, values);
            }
            response.Content = content;
        }

        foreach (var (name, values) in recorded.Headers)
        {
            response.Headers.TryAddWithoutValidation(name, values);
        }

        // Playback already has the next answer, so LRO poll delays are pure waste.
        response.Headers.Remove("Retry-After");
        response.Headers.TryAddWithoutValidation("Retry-After", "0");

        return response;
    }

    private string BuildUnmatchedDiagnostic(CapturedHttpRequest snapshot)
    {
        var builder = new StringBuilder()
            .AppendLine($"Playback found no recorded response (matching on {_matcher.Description}) for:")
            .AppendLine($"  {snapshot.Method} {snapshot.Uri}");

        var unconsumed = SessionRecord.Entries
            .Where((_, index) => !_consumed.Contains(index))
            .ToList();
        if (unconsumed.Count == 0)
        {
            builder.AppendLine($"The session record has no unconsumed entries ({SessionRecord.Entries.Count} total, all consumed or none recorded).");
        }
        else
        {
            builder.AppendLine($"Unconsumed recorded entries ({unconsumed.Count} of {SessionRecord.Entries.Count}):");
            foreach (var entry in unconsumed)
            {
                builder.AppendLine($"  {entry.Request.Method} {entry.Request.Uri}");
            }
        }

        builder.Append("Re-record the session (RecorderMode.Record) or align the request with the recording.");
        return builder.ToString();
    }
}
