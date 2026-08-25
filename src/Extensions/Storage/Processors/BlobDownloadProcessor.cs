// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Core.Abstractions.Runtime;

using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class BlobDownloadProcessor : StorageBlobProcessorBase
{
    public string? AbsoluteUri { get; set; }
    public string? SnapshotTime { get; set; }
    public string? VersionId { get; set; }
    public string? TagCondition { get; set; }

    public override async Task ProcessAsync()
    {
        using HttpResponseMessage response = await SendAsync();
        await response.EnsureSuccessAsync(Publisher);
    }

    internal async Task<HttpResponseMessage> SendAsync()
    {
        Uri uri = BuildSourceUri();
        bool directUri = !string.IsNullOrWhiteSpace(AbsoluteUri);
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, uri, requireProfile: !directUri);
        if (!string.IsNullOrWhiteSpace(TagCondition))
        {
            request.Headers.TryAddWithoutValidation("x-ms-if-tags", TagCondition);
        }
        HttpResponseMessage response = await SendAsync(request, requireProfile: !directUri);
        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized
            || ResolvedProfile is null
            || !uri.Host.StartsWith("md-", StringComparison.OrdinalIgnoreCase)
            || !TryGetAudience(response, out string audience))
        {
            return response;
        }

        response.Dispose();
        using HttpRequestMessage retry = CreateRequest(HttpMethod.Get, uri);
        retry.Options.Set(RuntimeHttpRequestOptions.Scopes, [$"{audience.TrimEnd('/')}/.default"]);
        if (!string.IsNullOrWhiteSpace(TagCondition))
        {
            retry.Headers.TryAddWithoutValidation("x-ms-if-tags", TagCondition);
        }
        return await SendAsync(retry);
    }

    internal Uri BuildSourceUri()
    {
        if (!string.IsNullOrWhiteSpace(AbsoluteUri))
        {
            if (!Uri.TryCreate(AbsoluteUri, UriKind.Absolute, out Uri? uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !IsAzureBlobHost(uri.Host))
            {
                throw new ArgumentException("AbsoluteUri must be an HTTPS Azure Blob Storage URL.", nameof(AbsoluteUri));
            }
            return uri;
        }

        if (!string.IsNullOrWhiteSpace(SnapshotTime) && !string.IsNullOrWhiteSpace(VersionId))
        {
            throw new ArgumentException("SnapshotTime and VersionId cannot be used together.");
        }
        if (!string.IsNullOrWhiteSpace(SnapshotTime)
            && !DateTimeOffset.TryParse(SnapshotTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
        {
            throw new ArgumentException("SnapshotTime must be a valid timestamp.", nameof(SnapshotTime));
        }

        Uri blobUri = BuildBlobUri();
        string? query = !string.IsNullOrWhiteSpace(SnapshotTime)
            ? $"snapshot={Uri.EscapeDataString(SnapshotTime)}"
            : !string.IsNullOrWhiteSpace(VersionId)
                ? $"versionid={Uri.EscapeDataString(VersionId)}"
                : null;
        return query is null ? blobUri : new UriBuilder(blobUri) { Query = query }.Uri;
    }

    private static bool IsAzureBlobHost(string host) =>
        host.EndsWith(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".blob.core.usgovcloudapi.net", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".blob.core.chinacloudapi.cn", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".blob.core.cloudapi.de", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetAudience(HttpResponseMessage response, out string audience)
    {
        audience = string.Empty;
        if (!response.Headers.TryGetValues("WWW-Authenticate", out IEnumerable<string>? values))
        {
            return false;
        }

        foreach (string value in values)
        {
            int start = value.IndexOf("resource_id=", StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                continue;
            }
            string candidate = value[(start + "resource_id=".Length)..].TrimStart('"');
            int end = candidate.IndexOfAny(['"', ' ', ',']);
            candidate = end < 0 ? candidate : candidate[..end];
            if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri)
                && uri.Scheme == Uri.UriSchemeHttps
                && string.IsNullOrEmpty(uri.UserInfo))
            {
                audience = uri.AbsoluteUri.TrimEnd('/');
                return true;
            }
        }
        return false;
    }
}
