// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Recording;

/// <summary>
/// Central sanitiser set. Named headers are dropped; every other URI, header value, and body is
/// rewritten. The same rewrites run over outgoing requests on playback, so one carrying a live
/// subscription id still matches its sanitised recording. Extend via <see cref="With"/>.
/// </summary>
public sealed class RecordingSanitizers
{
    /// <summary>Stand-in written in place of a real subscription or tenant id.</summary>
    public const string PlaceholderGuid = "00000000-0000-0000-0000-000000000000";

    private const string GuidPattern = "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}";

    public static RecordingSanitizers Default { get; } = new(
        [
            // subscription and tenant ids in URIs, bodies, and AAD authority URLs
            new RegexSanitizer("subscription-id", $"(?<=/subscriptions/){GuidPattern}", PlaceholderGuid),
            new RegexSanitizer("tenant-path-id", $"(?<=/tenants/){GuidPattern}", PlaceholderGuid),
            new RegexSanitizer("authority-tenant-id", $"(?<=login\\.microsoftonline\\.com/){GuidPattern}", PlaceholderGuid),
            new RegexSanitizer("tenant-id", $"(?<=\"tenantId\"\\s*:\\s*\"){GuidPattern}", PlaceholderGuid),
            new RegexSanitizer("principal-id", $"(?<=\"principalId\"\\s*:\\s*\"){GuidPattern}", PlaceholderGuid),
            new RegexSanitizer("client-id", $"(?<=\"clientId\"\\s*:\\s*\"){GuidPattern}", PlaceholderGuid),
            new RegexSanitizer("bearer-token", "Bearer\\s+[^\\s\"']+", "Bearer [redacted]"),
            // vm create carries the admin password in the request body
            new RegexSanitizer("password", @"(""(?:adminPassword|password)""\s*:\s*"")[^""]+", "$1[redacted]"),
            // agent traffic is out of scope, but scrub OpenAI secrets if any is ever recorded
            new RegexSanitizer("openai-key", "sk-[A-Za-z0-9_\\-]{16,}", "[redacted-openai-key]"),
            new RegexSanitizer("openai-endpoint", "https://[a-z0-9][a-z0-9-]*\\.openai\\.azure\\.com", "https://[resource].openai.azure.com"),
        ],
        // x-ms-operation-identifier is ARM telemetry carrying tenantId=...,objectId=... pairs.
        ["Authorization", "api-key", "x-ms-operation-identifier"]);

    public RecordingSanitizers(IReadOnlyList<IRecordingSanitizer> sanitizers, IReadOnlyList<string> removedHeaders)
    {
        Sanitizers = sanitizers;
        RemovedHeaders = new HashSet<string>(removedHeaders, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IRecordingSanitizer> Sanitizers { get; }

    /// <summary>Headers dropped from recordings entirely, rather than rewritten.</summary>
    public IReadOnlySet<string> RemovedHeaders { get; }

    /// <summary>Returns a copy with extra sanitisers and removed headers added.</summary>
    public RecordingSanitizers With(IEnumerable<IRecordingSanitizer>? sanitizers = null, IEnumerable<string>? removedHeaders = null)
        => new([.. Sanitizers, .. (sanitizers ?? [])], [.. RemovedHeaders, .. (removedHeaders ?? [])]);

    /// <summary>Runs every value sanitiser over a single string.</summary>
    public string Apply(string? value)
    {
        var sanitized = value ?? string.Empty;
        foreach (var sanitizer in Sanitizers)
        {
            sanitized = sanitizer.Sanitize(sanitized);
        }
        return sanitized;
    }

    public RecordedRequest Sanitize(RecordedRequest request) => new()
    {
        Method = request.Method,
        Uri = Apply(request.Uri),
        Headers = SanitizeHeaders(request.Headers, dropRemoved: true),
        Body = request.Body is null ? null : Apply(request.Body),
    };

    public RecordedResponse Sanitize(RecordedResponse response) => new()
    {
        StatusCode = response.StatusCode,
        Headers = SanitizeHeaders(response.Headers, dropRemoved: true),
        ContentHeaders = SanitizeHeaders(response.ContentHeaders, dropRemoved: true),
        Body = response.Body is null ? null : Apply(response.Body),
    };

    /// <summary>Sanitises an outgoing request the same way as the recording, so playback matches.</summary>
    public CapturedHttpRequest Normalize(CapturedHttpRequest request) => request with
    {
        Uri = Apply(request.Uri),
        Body = request.Body is null ? null : Apply(request.Body),
    };

    private Dictionary<string, string[]> SanitizeHeaders(IReadOnlyDictionary<string, string[]> headers, bool dropRemoved)
    {
        var sanitized = new Dictionary<string, string[]>();
        foreach (var (name, values) in headers)
        {
            if (dropRemoved && RemovedHeaders.Contains(name))
            {
                continue;
            }
            sanitized[name] = [.. values.Select(Apply)];
        }
        return sanitized;
    }
}
