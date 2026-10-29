// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Tests.Recording;

/// <summary>
/// Central sanitiser set. Named headers are dropped; every other URI, header value, and body is
/// rewritten. The same rewrites run over outgoing requests on playback, so one carrying a live
/// subscription id still matches its sanitised recording. Extend via <see cref="With"/>.
/// </summary>
public sealed class RecordingSanitizers
{
    public const string PlaceholderGuid = "00000000-0000-0000-0000-000000000000";

    /// <summary>The value recorded in place of a Storage account key or user delegation key.</summary>
    public const string FakeStorageKey = "veryFakedStorageAccountKey==";

    /// <summary>The object id recorded in place of a user delegation key's signed object id.</summary>
    public const string FakeObjectId = "00000000-0000-0000-0000-000000000001";

    private const string GuidPattern = "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}";

    public static RecordingSanitizers Default { get; } = new(
        [
            new RegexSanitizer("subscription-id", $"(?<=/subscriptions/){GuidPattern}", PlaceholderGuid),
            new RegexSanitizer("tenant-path-id", $"(?<=/tenants/){GuidPattern}", PlaceholderGuid),
            new RegexSanitizer("authority-tenant-id", $"(?<=login\\.microsoftonline\\.com/){GuidPattern}", PlaceholderGuid),
            new RegexSanitizer("tenant-id", $"(?<=\"tenantId\"\\s*:\\s*\"){GuidPattern}", PlaceholderGuid),
            new RegexSanitizer("principal-id", $"(?<=\"principalId\"\\s*:\\s*\"){GuidPattern}", PlaceholderGuid),
            // ARM async-operation URLs carry an opaque signed blob (c=certificate, s=signature,
            // h=hash). It is not a credential, but it trips the JWT rule in the pre-merge secret
            // scan and adds tens of kilobytes of churn to every recorded poll.
            new RegexSanitizer("arm-operation-signature", "(?<=[?&](?:c|s|h)=)[A-Za-z0-9_\\-%.=]{40,}", "[redacted]"),
            new RegexSanitizer("client-id", $"(?<=\"clientId\"\\s*:\\s*\"){GuidPattern}", PlaceholderGuid),
            new RegexSanitizer("bearer-token", "Bearer\\s+[^\\s\"']+", "Bearer [redacted]"),
            // ARM long-running operations return opaque polling tokens.
            new RegexSanitizer("operation-token", @"(?<=/operation(?:results|statuses)/)[A-Za-z0-9_\-%.=]+", "sanitized-operation-token"),
            // vm create carries the admin password in the request body
            new RegexSanitizer("password", @"(""(?:adminPassword|password)""\s*:\s*"")[^""]+", "$1[redacted]"),
            // run-scoped names: scenario-test-rg-t3f8a21c9 is recorded as scenario-test-rg
            new RegexSanitizer("run-scoped-name", "(scenario-[a-z-]+)-t[0-9a-f]{8}(?![0-9a-f])", "$1"),
            // agent traffic is out of scope, but scrub OpenAI secrets if any is ever recorded
            new RegexSanitizer("openai-key", "sk-[A-Za-z0-9_\\-]{16,}", "[redacted-openai-key]"),
            // Storage SAS: signature and signed key ids in URIs, x-ms-copy-source and echoed XML (&amp;).
            new RegexSanitizer("sas-signature", @"(?<=(?:[?&]|\u0026|&amp;|%26)sig(?:=|%3D))[^&""\<\s]+", "[redacted]"),
            new RegexSanitizer("sas-key-ids", $@"(?<=(?:[?&]|\u0026|&amp;|%26)s(?:koid|ktid|aoid|uoid|duoid|cid)(?:=|%3D)){GuidPattern}", PlaceholderGuid),
            // Storage account keys and the user delegation key become one fake Base64 value, as azure-cli's
            // StorageAccountKeyReplacer does, so playback can still decode and sign with them.
            new RegexSanitizer("storage-account-key", @"(?<=""keyName""\s*:\s*""[^""]+""\s*,\s*""value""\s*:\s*"")[^""]+", FakeStorageKey),
            new RegexSanitizer("delegation-key-value", "(?<=<Value>)[^<]+(?=</Value>)", FakeStorageKey),
            // The signed object id must not be empty, so it gets its own fixed id; the tenant matches the placeholder profile.
            new RegexSanitizer("delegation-key-oid", $"(?<=<SignedOid>){GuidPattern}", FakeObjectId),
            new RegexSanitizer("delegation-key-ids", $"(?<=<Signed(?:Tid|DelegatedUserTid)>){GuidPattern}", PlaceholderGuid),
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

    public IReadOnlySet<string> RemovedHeaders { get; }

    public RecordingSanitizers With(IEnumerable<IRecordingSanitizer>? sanitizers = null, IEnumerable<string>? removedHeaders = null)
        => new([.. Sanitizers, .. (sanitizers ?? [])], [.. RemovedHeaders, .. (removedHeaders ?? [])]);

    public string Apply(string? value)
    {
        var sanitized = value ?? string.Empty;
        Sanitizers.ToList().ForEach(sanitizer => sanitized = sanitizer.Sanitize(sanitized));
        return sanitized;
    }

    public RecordedRequest Sanitize(RecordedRequest request) => new()
    {
        Method = request.Method,
        Uri = Apply(request.Uri),
        Headers = SanitizeHeaders(request.Headers, dropRemoved: true),
        Body = request.Body is null ? null : Apply(request.Body),
        BodyBase64 = request.BodyBase64,
    };

    public RecordedResponse Sanitize(RecordedResponse response) => new()
    {
        StatusCode = response.StatusCode,
        Headers = SanitizeHeaders(response.Headers, dropRemoved: true),
        ContentHeaders = SanitizeHeaders(response.ContentHeaders, dropRemoved: true),
        Body = response.Body is null ? null : Apply(response.Body),
        BodyBase64 = response.BodyBase64,
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
        headers.ToList().ForEach(headers =>
        {
            var (name, values) = headers;
            if (!(dropRemoved && RemovedHeaders.Contains(name))) sanitized[name] = [.. values.Select(Apply)];
        });
        return sanitized;
    }
}
