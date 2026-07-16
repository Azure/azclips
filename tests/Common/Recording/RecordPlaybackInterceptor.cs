// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Text;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Tests.Common.Recording;

public enum RecorderMode
{
    /// <summary>Send requests through the real pipeline tail and capture each exchange into the session record.</summary>
    Record,

    /// <summary>Serve responses from the session record; the network is never touched.</summary>
    Playback,
}

/// <summary>
/// The record/playback <c>SendAsyncStep</c> for <c>IRuntimeHttpPipeline</c>, occupying the seam
/// <c>Engine.CreateProcessorContext</c> reserves at the tracing step ("record/playback request
/// during test"). Register it as the <see cref="ITracingInterceptor"/> singleton before
/// <c>AzclipsServiceProvider.Initialize</c>.
/// <para>
/// <b>Record:</b> forwards to the next step (retries → wire), then appends the request/response
/// pair to <see cref="SessionRecord"/>. The stored request omits the <c>Authorization</c> header
/// so bearer tokens never land on disk (the full sanitizer set is #473).
/// <b>Playback:</b> matches the outgoing request against the record by method + URI
/// (case-insensitive, each entry consumed once, duplicates served in recorded order) and returns
/// the stored response without calling the next step. An unmatched request throws with the
/// request line and the record's unconsumed entries so the mismatch is immediately visible.
/// </para>
/// </summary>
public sealed class RecordPlaybackInterceptor(RecorderMode mode, SessionRecord sessionRecord) : ITracingInterceptor
{
    private readonly List<CapturedHttpRequest> _sent = [];
    private readonly HashSet<int> _consumed = [];

    public RecorderMode Mode { get; } = mode;

    public SessionRecord SessionRecord { get; } = sessionRecord;

    /// <summary>Every request that reached this step, in send order (kept in memory only, unsanitized).</summary>
    public IReadOnlyList<CapturedHttpRequest> Sent => _sent;

    public SendAsyncStep SendAsync => async (HttpRequestMessage request, IEventPublisher callback, ISendAsync next) =>
    {
        var snapshot = await CapturedHttpRequest.SnapshotAsync(request);
        _sent.Add(snapshot);
        return Mode == RecorderMode.Record
            ? await RecordAsync(snapshot, request, callback, next)
            : Playback(snapshot);
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

        SessionRecord.Entries.Add(new SessionRecordEntry
        {
            Request = new RecordedRequest
            {
                Method = snapshot.Method,
                Uri = snapshot.Uri,
                Headers = snapshot.Headers
                    .Where(header => !string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(header => header.Key, header => header.Value),
                Body = snapshot.Body,
            },
            Response = new RecordedResponse
            {
                StatusCode = (int)response.StatusCode,
                Headers = responseHeaders,
                ContentHeaders = contentHeaders,
                Body = responseBody,
            },
        });

        return response;
    }

    private HttpResponseMessage Playback(CapturedHttpRequest snapshot)
    {
        for (int i = 0; i < SessionRecord.Entries.Count; i++)
        {
            var entry = SessionRecord.Entries[i];
            if (!_consumed.Contains(i) && Matches(entry.Request, snapshot))
            {
                _consumed.Add(i);
                return ToResponse(entry.Response);
            }
        }

        throw new InvalidOperationException(BuildUnmatchedDiagnostic(snapshot));
    }

    private static bool Matches(RecordedRequest recorded, CapturedHttpRequest outgoing)
        => string.Equals(recorded.Method, outgoing.Method, StringComparison.OrdinalIgnoreCase)
        && string.Equals(recorded.Uri, outgoing.Uri, StringComparison.OrdinalIgnoreCase);

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

        return response;
    }

    private string BuildUnmatchedDiagnostic(CapturedHttpRequest snapshot)
    {
        var builder = new StringBuilder()
            .AppendLine("Playback found no recorded response for:")
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
