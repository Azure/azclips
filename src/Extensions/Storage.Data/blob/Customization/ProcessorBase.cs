// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace  Microsoft.Azure.AzClips.Extensions.StorageBlob.Processors
{
    /// <summary>
    /// Hand-authored Shared Key auth for StorageBlob processors. Bearer token acquisition is replaced with a no-op,
    /// and a pipeline step signs every request with <see cref="StorageAccountKey"/>.
    /// </summary>
    /// <remarks>
    /// Lives under <c>Customization/</c> so codegen won't overwrite it.
    /// </remarks>
    public abstract partial class ProcessorBase<TRequest, TResponse>
        where TRequest : class
        where TResponse : class
    {
        private const string SharedKeyScheme = "SharedKey";

        /// <summary>Storage account access key (base64) used to sign requests with Shared Key.</summary>
        public string StorageAccountKey { get; set; } = string.Empty;

        partial void OverrideBeforeProcessBase()
        {
            if (string.IsNullOrWhiteSpace(StorageAccountKey))
            {
                throw new InvalidOperationException($"{nameof(StorageAccountKey)} is required.");
            }

            // The engine's bearer step still runs when a profile exists; skip token acquisition and let the
            // Shared Key step overwrite the Authorization header.
            static Task<AuthResult> NoTokenAsync(string[]? _, string? tenantId, bool promptOnError, CancellationToken cancellationToken, Profile? profile) =>
                Task.FromResult(new AuthResult
                {
                    AccessToken = string.Empty,
                    ExpiresOn = DateTimeOffset.MaxValue,
                    Scopes = [],
                    TenantId = tenantId ?? string.Empty,
                });

            AuthHandler.AcquireTokenAsyncOverride = NoTokenAsync;
            AuthHandler.ApplyOverride = true;

            // The pipeline is shared across processors in one invocation (including paging and child processors);
            // the step skips requests that are already signed, so appending once per processor stays safe.
            string accountKey = StorageAccountKey;
            HttpPipeline.Append((request, callback, next) =>
            {
                if (request.Headers.Authorization?.Scheme != SharedKeyScheme)
                {
                    SignWithSharedKey(request, accountKey);
                }
                return next.SendAsync(request, callback);
            });
        }

        /// <summary>
        /// Signs the request per https://learn.microsoft.com/rest/api/storageservices/authorize-with-shared-key.
        /// Must run after every other step that adds headers.
        /// </summary>
        private static void SignWithSharedKey(HttpRequestMessage request, string accountKey)
        {
            Uri uri = request.RequestUri ?? throw new InvalidOperationException("Request URI is not set.");
            string accountName = uri.Host.Split('.')[0];

            request.Headers.Remove("x-ms-date");
            request.Headers.TryAddWithoutValidation("x-ms-date", DateTimeOffset.UtcNow.ToString("R", CultureInfo.InvariantCulture));

            long? contentLength = request.Content?.Headers.ContentLength;
            var stringToSign = new StringBuilder()
                .Append(request.Method.Method.ToUpperInvariant()).Append('\n')
                .Append(GetHeader(request, "Content-Encoding")).Append('\n')
                .Append(GetHeader(request, "Content-Language")).Append('\n')
                .Append(contentLength is null or 0 ? string.Empty : contentLength.Value.ToString(CultureInfo.InvariantCulture)).Append('\n')
                .Append(GetHeader(request, "Content-MD5")).Append('\n')
                .Append(GetHeader(request, "Content-Type")).Append('\n')
                .Append(string.Empty).Append('\n') // Date: x-ms-date is used instead
                .Append(GetHeader(request, "If-Modified-Since")).Append('\n')
                .Append(GetHeader(request, "If-Match")).Append('\n')
                .Append(GetHeader(request, "If-None-Match")).Append('\n')
                .Append(GetHeader(request, "If-Unmodified-Since")).Append('\n')
                .Append(GetHeader(request, "Range")).Append('\n')
                .Append(GetCanonicalizedHeaders(request))
                .Append(GetCanonicalizedResource(uri, accountName))
                .ToString();

            using var hmac = new HMACSHA256(Convert.FromBase64String(accountKey));
            string signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            request.Headers.Authorization = new AuthenticationHeaderValue(SharedKeyScheme, $"{accountName}:{signature}");
        }

        private static string GetHeader(HttpRequestMessage request, string name)
        {
            if (request.Headers.NonValidated.TryGetValues(name, out HeaderStringValues values) ||
                (request.Content is not null && request.Content.Headers.NonValidated.TryGetValues(name, out values)))
            {
                return string.Join(",", values);
            }
            return string.Empty;
        }

        private static string GetCanonicalizedHeaders(HttpRequestMessage request)
        {
            IEnumerable<KeyValuePair<string, HeaderStringValues>> headers = request.Headers.NonValidated;
            if (request.Content is not null)
            {
                headers = headers.Concat(request.Content.Headers.NonValidated);
            }

            var builder = new StringBuilder();
            foreach (var header in headers
                .Where(h => h.Key.StartsWith("x-ms-", StringComparison.OrdinalIgnoreCase))
                .Select(h => (Name: h.Key.ToLowerInvariant(), Value: string.Join(",", h.Value).Trim()))
                .OrderBy(h => h.Name, StringComparer.Ordinal))
            {
                builder.Append(header.Name).Append(':').Append(header.Value).Append('\n');
            }
            return builder.ToString();
        }

        private static string GetCanonicalizedResource(Uri uri, string accountName)
        {
            var builder = new StringBuilder("/").Append(accountName).Append(uri.AbsolutePath);

            var parameters = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = pair.IndexOf('=');
                string name = Uri.UnescapeDataString(separator < 0 ? pair : pair[..separator]).ToLowerInvariant();
                string value = separator < 0 ? string.Empty : Uri.UnescapeDataString(pair[(separator + 1)..]);
                if (!parameters.TryGetValue(name, out List<string>? list))
                {
                    parameters[name] = list = [];
                }
                list.Add(value);
            }

            foreach (var parameter in parameters)
            {
                parameter.Value.Sort(StringComparer.Ordinal);
                builder.Append('\n').Append(parameter.Key).Append(':').Append(string.Join(",", parameter.Value));
            }
            return builder.ToString();
        }
    }
}
