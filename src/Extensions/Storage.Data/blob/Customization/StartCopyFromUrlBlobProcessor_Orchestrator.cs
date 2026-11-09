// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Core.Abstractions.Runtime;
using Microsoft.Azure.AzClips.Extensions.StorageBlob.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageBlob.Processors
{
    /// <summary>
    /// Start Copy From URL on top of the generated <see cref="StartCopyFromUrlBlobProcessor"/>, which needs
    /// hand-written wire formats the spec leaves to the client:
    /// <list type="bullet">
    /// <item><description>metadata is one <c>x-ms-meta-{key}</c> header per pair, not the single <c>x-ms-meta</c> the spec declares;</description></item>
    /// <item><description><c>x-ms-tags</c> is a URL-encoded query string;</description></item>
    /// <item><description><c>x-ms-copy-source</c> must be URL-encoded.</description></item>
    /// </list>
    /// The response is headers only and is mapped to <see cref="BlobCopyResult"/>.
    /// </summary>
    /// <remarks>
    /// The source is read by the service, not signed by this request: it must be public, in the destination
    /// account, or carry a SAS in <see cref="SourceUrl"/>.
    /// </remarks>
    public class StartCopyFromUrlBlobProcessor_Orchestrator : ProcessorBase<object, BlobCopyResult>, ISasAuthorized
    {
        private const string MetadataPrefix = "x-ms-meta-";

        /// <summary>SAS for the destination; a SAS for the source belongs in <see cref="SourceUrl"/>.</summary>
        public string? SasToken { get; set; }

        /// <summary>The source blob URL, unencoded or encoded; a SAS query string is kept as given.</summary>
        public string SourceUrl { get; set; } = string.Empty;

        /// <summary>Destination container name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ContainerName { get; set; }

        /// <summary>Destination blob name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? BlobName { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ContainerName), ContainerName), (nameof(BlobName), BlobName));

        /// <summary>Destination metadata as <c>key=value</c> pairs.</summary>
        public string[]? Metadata { get; set; }

        /// <summary>Destination tags as <c>key=value</c> pairs.</summary>
        public string[]? Tags { get; set; }

        public int? Timeout { get; set; }
        public string? ClientRequestId { get; set; }
        public string? Tier { get; set; }
        public string? RehydratePriority { get; set; }
        public string? SourceIfModifiedSince { get; set; }
        public string? SourceIfUnmodifiedSince { get; set; }
        public string? SourceIfMatch { get; set; }
        public string? SourceIfNoneMatch { get; set; }
        public string? SourceIfTags { get; set; }
        public string? IfModifiedSince { get; set; }
        public string? IfUnmodifiedSince { get; set; }
        public string? IfNoneMatch { get; set; }
        public string? IfMatch { get; set; }
        public string? IfTags { get; set; }
        public string? LeaseId { get; set; }
        public bool? SealBlob { get; set; }
        public string? ImmutabilityPolicyExpiry { get; set; }
        public string? ImmutabilityPolicyMode { get; set; }
        public bool? LegalHold { get; set; }

        public override async Task ProcessAsync()
        {
            try
            {
                await BeforeProcessAsync();
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);
                Dictionary<string, string> metadata = ParsePairs(Metadata, nameof(Metadata));
                Dictionary<string, string> tags = ParsePairs(Tags, nameof(Tags));
                using HttpResponseMessage response = await CreateExtendedProcessor<StartCopyRequest>(p =>
                {
                    p.Url = Endpoint;
                    p.CopySource = EncodeUrl(SourceUrl);
                    p.MetadataPairs = metadata;
                    p.BlobTagsString = tags.Count > 0
                        ? string.Join("&", tags.Select(t => $"{Uri.EscapeDataString(t.Key)}={Uri.EscapeDataString(t.Value)}"))
                        : null;
                    p.Timeout = Timeout;
                    p.ClientRequestId = ClientRequestId;
                    p.Tier = Tier;
                    p.RehydratePriority = RehydratePriority;
                    p.SourceIfModifiedSince = SourceIfModifiedSince;
                    p.SourceIfUnmodifiedSince = SourceIfUnmodifiedSince;
                    p.SourceIfMatch = SourceIfMatch;
                    p.SourceIfNoneMatch = SourceIfNoneMatch;
                    p.SourceIfTags = SourceIfTags;
                    p.IfModifiedSince = IfModifiedSince;
                    p.IfUnmodifiedSince = IfUnmodifiedSince;
                    p.IfNoneMatch = IfNoneMatch;
                    p.IfMatch = IfMatch;
                    p.IfTags = IfTags;
                    p.LeaseId = LeaseId;
                    p.SealBlob = SealBlob;
                    p.ImmutabilityPolicyExpiry = ImmutabilityPolicyExpiry;
                    p.ImmutabilityPolicyMode = ImmutabilityPolicyMode;
                    p.LegalHold = LegalHold;
                }).SendAsync();
                await WriteOutput(ToResult(response));
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        /// <summary>
        /// Escapes what a request URI cannot carry (spaces, non-ASCII) while keeping existing escapes and the
        /// query string as given, so an encoded URL or a SAS signature passes through unchanged.
        /// </summary>
        private static string EncodeUrl(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                ? uri.AbsoluteUri
                : throw new ArgumentException($"'{url}' is not an absolute URL.", nameof(SourceUrl));

        private static Dictionary<string, string> ParsePairs(string[]? pairs, string parameterName)
        {
            Dictionary<string, string> result = new(StringComparer.Ordinal);
            foreach (string pair in pairs ?? [])
            {
                int separator = pair.IndexOf('=');
                if (separator <= 0)
                {
                    throw new ArgumentException($"'{pair}' is not a key=value pair.", parameterName);
                }
                result[pair[..separator]] = pair[(separator + 1)..];
            }
            return result;
        }

        private static BlobCopyResult ToResult(HttpResponseMessage response)
        {
            // Last-Modified is a content header in HttpClient even when the body is empty.
            Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers.Concat(response.Content.Headers))
            {
                headers[header.Key] = string.Join(",", header.Value);
            }
            string? Header(string name) => headers.TryGetValue(name, out string? value) && value.Length > 0 ? value : null;

            return new BlobCopyResult
            {
                CopyId = Header("x-ms-copy-id"),
                CopyStatus = Header("x-ms-copy-status"),
                ETag = Header("ETag"),
                LastModified = Header("Last-Modified"),
                VersionId = Header("x-ms-version-id"),
            };
        }

        /// <summary>Sends metadata as one <c>x-ms-meta-{key}</c> header per pair instead of the generated single header.</summary>
        private sealed class StartCopyRequest : StartCopyFromUrlBlobProcessor
        {
            public IReadOnlyDictionary<string, string> MetadataPairs { get; set; } = new Dictionary<string, string>();

            protected override void CreateRequestHeaders(HttpRequestMessage request)
            {
                base.CreateRequestHeaders(request);
                foreach (KeyValuePair<string, string> pair in MetadataPairs)
                {
                    request.Headers.TryAddWithoutValidation(MetadataPrefix + pair.Key, pair.Value);
                }
            }
        }
    }
}
