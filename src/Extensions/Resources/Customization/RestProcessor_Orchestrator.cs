// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Universal authenticated HTTP passthrough, mirroring <c>az rest</c> (Option 2 parity): no LRO
    /// polling, no pagination, ARM audience only. Builds an arbitrary request from the supplied
    /// method/uri/payload/headers, sends it through the already-authenticated pipeline, and surfaces
    /// the parsed JSON body (or raw text), with an optional full <see cref="Models.RestResponse"/>. This is
    /// a handwritten customization (it overrides <see cref="ProcessAsync"/> with bespoke logic rather
    /// than being a generated atomic API operation), so it lives under <c>Customization/</c>.
    /// <para>
    /// Unlike the typed Resources base, this processor is a standalone <see cref="IProcessor"/> that
    /// does NOT deserialize the response into a fixed model. Its transport primitive
    /// <see cref="SendRequestAsync"/> does not throw on non-2xx (it returns the response as-is) so the
    /// selected response payload can be surfaced to output first.
    /// <see cref="ProcessAsync"/> writes that payload to output and then, for a status
    /// &gt;= 400, publishes a failure event and throws an
    /// <see cref="System.Net.Http.HttpRequestException"/> so the command produces a non-zero,
    /// terminating outcome after the error body has already been made visible.
    /// </para>
    /// </summary>
    public class RestProcessor_Orchestrator : IProcessor
    {
        private const string ArmEndpoint = "https://management.azure.com";
        private const string SubscriptionToken = "{subscriptionId}";

        // ── Runtime context (captured in Init) ──
        protected IRuntimeHttpPipeline HttpPipeline = null!;
        protected IEventPublisher Publisher = null!;
        protected JsonSerializerOptions JsonOptions = null!;
        protected OutputStreamDelegate WriteOutput = null!;
        protected Profile ResolvedProfile = null!;

        /// <summary>
        /// Exists only to accept the value during binding — the Engine has already resolved it into
        /// <see cref="ResolvedProfile"/>. Per-processor workaround until global parameters exist.
        /// </summary>
        public string? Profile { get; set; }

        /// <summary>
        /// ARM / Azure management endpoints the rest command is allowed to target. The pipeline's
        /// <c>AuthHandler</c> attaches the ARM bearer token to every outgoing request regardless of
        /// host, so an absolute URL pointing anywhere else would leak a valid ARM token. Any absolute
        /// URL must resolve to one of these hosts (public, US Gov, China, legacy Germany clouds).
        /// </summary>
        private static readonly HashSet<string> AllowedArmHosts = new(StringComparer.OrdinalIgnoreCase)
        {
            "management.azure.com",
            "management.usgovcloudapi.net",
            "management.chinacloudapi.cn",
            "management.microsoftazure.de",
        };

        /// <summary>HTTP method. Case-insensitive. Defaults to GET.</summary>
        public string Method { get; set; } = "GET";

        /// <summary>Absolute URL, or a relative ARM path (prefixed with the ARM endpoint).</summary>
        public string Uri { get; set; } = "";

        /// <summary>Optional request body (typically JSON); sent whenever provided, for any method.</summary>
        public string Payload { get; set; } = "";

        /// <summary>Optional user headers in KEY=VALUE form.</summary>
        public List<string> Headers { get; set; } = [];

        /// <summary>Return status, headers, raw content, and parsed JSON instead of only the body.</summary>
        public bool FullResponse { get; set; }

        // ── ByParameters mode (piece-wise ARM URL builder) ──
        // The following inputs are an alternative to <see cref="Uri"/>: instead of supplying a full
        // URL, the caller specifies the ARM resource id pieces and an api-version, and the processor
        // builds the relative ARM path. Mirrors Invoke-AzRestMethod's ByParameters parameter set.

        /// <summary>
        /// Subscription id used when building a ByParameters URL. Defaults to the current auth
        /// context subscription when omitted. Pure URL-building input — does NOT change auth.
        /// </summary>
        public string SubscriptionId { get; set; } = "";

        /// <summary>Resource group name segment for a ByParameters URL.</summary>
        public string ResourceGroupName { get; set; } = "";

        /// <summary>Resource provider namespace (e.g. Microsoft.Resources) for a ByParameters URL.</summary>
        public string ResourceProviderName { get; set; } = "";

        /// <summary>Resource type segments, interleaved with <see cref="Name"/>.</summary>
        public List<string> ResourceType { get; set; } = [];

        /// <summary>Resource name segments, interleaved with <see cref="ResourceType"/>.</summary>
        public List<string> Name { get; set; } = [];

        /// <summary>api-version for a ByParameters URL. Required in ByParameters mode.</summary>
        public string ApiVersion { get; set; } = "";

        /// <summary>Optional query-string parameters in KEY=VALUE form, appended to the final URL.</summary>
        public List<string> UriParameters { get; set; } = [];

        public CancellationToken Token => CancellationToken.None;

        public Action Cancel => throw new NotImplementedException();

        public IProcessor Init(ProcessorContext context)
        {
            // TODO: temporarily initialize arbitrary json options here, get from context in the future
            JsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            HttpPipeline = context.HttpPipeline;
            Publisher = context.Publisher;
            WriteOutput = context.OutputHandler.WriteOutput;
            ResolvedProfile = context.Profile ?? throw AzclipsErrorException.NotSignedIn("No profile is available. Please sign in.");
            return this;
        }

        public async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            try
            {
                using HttpRequestMessage request = await CreateRequestMessage();
                using HttpResponseMessage response = await SendRequestAsync(request);
                Models.RestResponse result = await BuildResponseAsync(response);
                object? output = SelectOutput(result);
                if (output is not null)
                    await WriteOutput(output);

                // Mirror az rest: the selected response output is surfaced above,
                // but an HTTP error must still produce a non-zero CLI exit so scripts using set -e / $?
                // can detect it. Publish the failure to the error stream and throw AFTER WriteOutput so
                // the body stays visible. Reuse the same channel typed processors use (EnsureSuccessAsync).
                if (result.StatusCode >= 400)
                {
                    string uri = response.RequestMessage?.RequestUri?.ToString() ?? "(unknown)";
                    string message = $"{result.StatusCode} {response.ReasonPhrase} from {uri}";
                    await Publisher.Publish(Events.ResourceOperationFailed, message);
                    throw new HttpRequestException(message, inner: null, statusCode: response.StatusCode);
                }
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        private object? SelectOutput(Models.RestResponse response)
        {
            if (FullResponse)
                return response;

            if (response.Content.Length == 0)
                return null;

            return response.Json.HasValue ? (object)response.Json.Value : response.Content;
        }

        /// <summary>
        /// Sends <paramref name="request"/> through the authenticated pipeline (the auth handler is
        /// already prepended by the engine, so this command only targets the ARM audience) and
        /// returns the raw response. The caller owns the returned <see cref="HttpResponseMessage"/>
        /// and must dispose it. Non-2xx responses are returned as-is (not thrown) so error payloads
        /// can be surfaced.
        /// </summary>
        internal async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request)
        {
            HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
            await Publisher.Publish(Events.TracingHttpResponseMessage, response);
            return response;
        }

        /// <summary>
        /// Normalizes a KEY=VALUE argument (headers or uri-parameters) to a <c>string[]</c> of
        /// <c>KEY=VALUE</c> entries, accepting either a hashtable/dictionary or an existing string collection.
        /// </summary>
        public static string[] NormalizeKeyValue(object value)
        {
            if (value is IDictionary dictionary)
            {
                var entries = new List<string>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    entries.Add($"{entry.Key}={entry.Value}");
                }
                return entries.ToArray();
            }

            if (value is string single)
            {
                return [single];
            }

            if (value is IEnumerable enumerable)
            {
                var entries = new List<string>();
                foreach (var item in enumerable)
                {
                    entries.Add(item?.ToString() ?? string.Empty);
                }
                return entries.ToArray();
            }

            return [value.ToString() ?? string.Empty];
        }

        private async Task<HttpRequestMessage> CreateRequestMessage()
        {
            HttpMethod method = ResolveMethod(Method);
            string url = AppendUriParameters(BuildUrl());

            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
            var request = new HttpRequestMessage(method, url);

            // Attach the request body whenever a payload is provided (any HTTP method). Default the
            // content type to application/json unless the user overrides it via a Content-Type header.
            if (!string.IsNullOrWhiteSpace(Payload))
            {
                request.Content = new StringContent(Payload, Encoding.UTF8)
                {
                    Headers = { ContentType = null }
                };
            }

            ApplyHeaders(request);

            if (request.Content is not null && request.Content.Headers.ContentType is null)
            {
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

        private static HttpMethod ResolveMethod(string method)
        {
            string normalized = string.IsNullOrWhiteSpace(method) ? "GET" : method.Trim().ToUpperInvariant();
            return normalized switch
            {
                "GET" => HttpMethod.Get,
                "POST" => HttpMethod.Post,
                "PUT" => HttpMethod.Put,
                "PATCH" => HttpMethod.Patch,
                "DELETE" => HttpMethod.Delete,
                "HEAD" => HttpMethod.Head,
                "OPTIONS" => HttpMethod.Options,
                _ => throw AzclipsErrorException.InvalidInput($"Unsupported HTTP method '{method}'. Supported methods: GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS.")
            };
        }

        /// <summary>
        /// Selects the endpoint mode (URI vs piece-wise ByParameters), validates that exactly one is
        /// supplied, and returns the resolved absolute ARM URL (before any extra query parameters are
        /// appended). The two modes are mutually exclusive.
        /// </summary>
        private string BuildUrl()
        {
            bool hasByParameters = HasByParametersInput();
            bool hasUri = !string.IsNullOrWhiteSpace(Uri);

            if (hasUri && hasByParameters)
            {
                throw AzclipsErrorException.InvalidInput(
                    "Specify the endpoint either as a full URL (Uri) OR as the piece-wise parameters " +
                    "(SubscriptionId/ResourceGroupName/ResourceProviderName/ResourceType/Name/ApiVersion), not both.");
            }

            if (hasUri)
            {
                return ResolveUrl(Uri);
            }

            if (hasByParameters)
            {
                return ResolveUrl(BuildByParametersPath());
            }

            throw AzclipsErrorException.InvalidInput(
                "An endpoint is required: supply either a full URL (Uri) or the piece-wise ByParameters inputs " +
                "(SubscriptionId/ResourceGroupName/ResourceProviderName/ResourceType/Name/ApiVersion) to build an ARM URL.");
        }

        /// <summary>
        /// True when any ByParameters input is supplied (an explicit subscription id, a resource group,
        /// a resource provider, a resource type/name segment, or an api-version).
        /// </summary>
        private bool HasByParametersInput()
        {
            return !string.IsNullOrWhiteSpace(ResourceGroupName)
                || !string.IsNullOrWhiteSpace(ResourceProviderName)
                || ResourceType.Count > 0
                || Name.Count > 0
                // Yabo: comment this line out because
                // 1. susbscription now applied from profile by default
                // 2. will implement real parameter set in argument parsing service in the future
                // || !string.IsNullOrWhiteSpace(SubscriptionId)
                || !string.IsNullOrWhiteSpace(ApiVersion);
        }

        /// <summary>
        /// Builds a relative ARM path from the piece-wise ByParameters inputs, matching the ordering of
        /// Invoke-AzRestMethod's ByParameters set: subscription, optional resource group, optional
        /// provider, interleaved resource-type/name segments, and a required api-version query.
        /// </summary>
        private string BuildByParametersPath()
        {
            string subscriptionId = !string.IsNullOrWhiteSpace(SubscriptionId)
                ? SubscriptionId.Trim()
                : ResolvedProfile?.SubscriptionId ?? "";

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                throw AzclipsErrorException.NotSignedIn(
                    "No subscription is available to build the ARM URL. Supply SubscriptionId or sign in first.");
            }

            if (string.IsNullOrWhiteSpace(ApiVersion))
            {
                throw AzclipsErrorException.InvalidInput("ApiVersion is required when building an ARM URL from piece-wise parameters.");
            }

            if (Name.Count > ResourceType.Count || ResourceType.Count - Name.Count > 1)
            {
                throw AzclipsErrorException.InvalidInput(
                    $"Name ({Name.Count}) must equal ResourceType ({ResourceType.Count}) or be exactly one fewer " +
                    "(one trailing type without a name performs a collection GET on the last type).");
            }

            // Percent-encode every caller-supplied id segment as it is appended so that a value
            // containing reserved/structural characters ('/', '?', '#', ...) cannot malform the path
            // or inject extra path/query structure. Only the VALUES are escaped: the structural
            // separators the builder itself adds (/subscriptions/, /resourceGroups/, /providers/, the
            // interleaving slashes, and the ?api-version= query) are emitted literally. EscapeDataString
            // leaves unreserved characters (letters, digits, '-', '.', '_', '~') intact, so legitimate
            // values like a GUID subscription id, 'Microsoft.Storage', 'storageAccounts' or
            // 'azclips-test-group-1' are unchanged, while a stray '/' becomes %2F.
            var builder = new StringBuilder();
            builder.Append("/subscriptions/").Append(System.Uri.EscapeDataString(subscriptionId));

            if (!string.IsNullOrWhiteSpace(ResourceGroupName))
            {
                builder.Append("/resourceGroups/").Append(System.Uri.EscapeDataString(ResourceGroupName.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(ResourceProviderName))
            {
                builder.Append("/providers/").Append(System.Uri.EscapeDataString(ResourceProviderName.Trim()));
            }

            for (int i = 0; i < ResourceType.Count; i++)
            {
                string resourceTypeSegment = ResourceType[i].Trim().Trim('/');
                if (resourceTypeSegment.Length == 0)
                {
                    throw AzclipsErrorException.InvalidInput(
                        $"ResourceType segments must be non-empty; ResourceType element {i} was empty or whitespace.");
                }

                builder.Append('/').Append(System.Uri.EscapeDataString(resourceTypeSegment));
                if (i < Name.Count)
                {
                    string nameSegment = Name[i].Trim().Trim('/');
                    if (nameSegment.Length == 0)
                    {
                        throw AzclipsErrorException.InvalidInput(
                            $"Name segments must be non-empty; Name element {i} was empty or whitespace.");
                    }

                    builder.Append('/').Append(System.Uri.EscapeDataString(nameSegment));
                }
            }

            builder.Append("?api-version=").Append(System.Uri.EscapeDataString(ApiVersion.Trim()));
            return builder.ToString();
        }

        /// <summary>
        /// Appends any <see cref="UriParameters"/> (KEY=VALUE) to the resolved URL as query-string
        /// parameters, URL-encoding keys and values and choosing '?' or '&amp;' based on whether the URL
        /// already has a query string.
        /// </summary>
        private string AppendUriParameters(string url)
        {
            if (UriParameters.Count == 0)
            {
                return url;
            }

            var builder = new StringBuilder(url);
            bool hasQuery = url.Contains('?', StringComparison.Ordinal);

            foreach (string parameter in UriParameters)
            {
                if (string.IsNullOrWhiteSpace(parameter))
                {
                    throw AzclipsErrorException.InvalidInput($"Invalid uri parameter '{parameter}'. Expected KEY=VALUE format.");
                }

                int separator = parameter.IndexOf('=');
                if (separator <= 0)
                {
                    throw AzclipsErrorException.InvalidInput($"Invalid uri parameter '{parameter}'. Expected KEY=VALUE format.");
                }

                string key = parameter[..separator].Trim();
                string value = parameter[(separator + 1)..];

                if (string.IsNullOrWhiteSpace(key))
                {
                    throw AzclipsErrorException.InvalidInput($"Invalid uri parameter '{parameter}': parameter name cannot be empty.");
                }

                builder.Append(hasQuery ? '&' : '?');
                builder.Append(System.Uri.EscapeDataString(key));
                builder.Append('=');
                builder.Append(System.Uri.EscapeDataString(value));
                hasQuery = true;
            }

            return builder.ToString();
        }

        private string ResolveUrl(string uri)
        {
            string url = uri.Trim();

            // Substitute {subscriptionId} BEFORE resolving/validating the host so the final URL that
            // gets sent is the one that is host-checked.
            if (url.Contains(SubscriptionToken, StringComparison.Ordinal))
            {
                if (ResolvedProfile is null || string.IsNullOrWhiteSpace(ResolvedProfile.SubscriptionId))
                {
                    throw AzclipsErrorException.NotSignedIn($"The URL contains '{SubscriptionToken}' but no subscription is available in the active profile. Sign in first or use an explicit subscription id.");
                }

                url = url.Replace(SubscriptionToken, ResolvedProfile.SubscriptionId, StringComparison.Ordinal);
            }

            // A leading-slash path (e.g. "/subscriptions/.../resourcegroups") is, by REST/HTTP
            // convention, always a relative ARM path. Handle it explicitly BEFORE the absolute parse
            // below: on Unix, System.Uri.TryCreate(url, UriKind.Absolute, ...) interprets a
            // leading-slash path as an absolute "file://" URI and returns true (it does not on
            // Windows), which would misclassify a relative ARM path as a non-https absolute URL and
            // reject it. Resolving it here keeps behaviour identical across platforms.
            if (url.StartsWith('/'))
            {
                return $"{ArmEndpoint.TrimEnd('/')}/{url.TrimStart('/')}";
            }

            // Use a real URI parse (not a substring check on "://") to distinguish absolute from
            // relative: a relative ARM path can legitimately embed "://" in a query value (e.g. a
            // redirect/targetUri parameter), which a substring check would misclassify as absolute.
            if (!System.Uri.TryCreate(url, UriKind.Absolute, out System.Uri? parsed))
            {
                // Relative ARM path: prefix with the ARM endpoint (host trailing slash stripped,
                // single separator), matching az rest's send_raw_request. The resulting host is
                // management.azure.com, which is in the allowlist by construction.
                return $"{ArmEndpoint.TrimEnd('/')}/{url.TrimStart('/')}";
            }

            // Absolute URL: the AuthHandler attaches the ARM bearer token to every request regardless
            // of host, so restrict absolute URLs to https + an ARM management host to avoid leaking the
            // token to an arbitrary destination (token-exfiltration / SSRF).
            if (!parsed.Scheme.Equals(System.Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw AzclipsErrorException.InvalidInput($"Absolute URL '{url}' must use https. The rest command only targets ARM over https in this release.");
            }

            if (!AllowedArmHosts.Contains(parsed.Host))
            {
                throw AzclipsErrorException.InvalidInput($"Host '{parsed.Host}' is not an allowed Azure Resource Manager endpoint. The rest command only targets ARM in this release. Allowed hosts: {string.Join(", ", AllowedArmHosts.OrderBy(h => h, StringComparer.OrdinalIgnoreCase))}.");
            }

            return url;
        }

        private void ApplyHeaders(HttpRequestMessage request)
        {
            foreach (string header in Headers)
            {
                if (string.IsNullOrWhiteSpace(header))
                {
                    continue;
                }

                int separator = header.IndexOf('=');
                if (separator <= 0)
                {
                    throw AzclipsErrorException.InvalidInput($"Invalid header '{header}'. Expected KEY=VALUE format.");
                }

                string name = header[..separator].Trim();
                string value = header[(separator + 1)..];

                if (string.IsNullOrWhiteSpace(name))
                {
                    throw AzclipsErrorException.InvalidInput($"Invalid header '{header}': header name cannot be empty.");
                }

                if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    if (request.Content is not null)
                    {
                        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(value);
                    }
                    continue;
                }

                if (!request.Headers.TryAddWithoutValidation(name, value))
                {
                    // TryAddWithoutValidation skips header-name/value validation. CR/LF injection is
                    // still prevented because SocketsHttpHandler rejects header values containing CR/LF
                    // at send time.
                    request.Content?.Headers.TryAddWithoutValidation(name, value);
                }
            }
        }

        private async Task<Models.RestResponse> BuildResponseAsync(HttpResponseMessage response)
        {
            var result = new Models.RestResponse
            {
                StatusCode = (int)response.StatusCode
            };

            foreach (var header in response.Headers)
            {
                result.Headers[header.Key] = string.Join(", ", header.Value);
            }

            if (response.Content is not null)
            {
                foreach (var header in response.Content.Headers)
                {
                    result.Headers[header.Key] = string.Join(", ", header.Value);
                }

                string body = await response.Content.ReadAsStringAsync();
                result.Content = body;

                // Recognize JSON regardless of content type, like legacy az rest. Only report
                // invalid JSON when the service advertised JSON; other bodies are valid passthrough.
                string? mediaType = response.Content.Headers.ContentType?.MediaType;
                bool isJsonMediaType = mediaType is not null
                    && mediaType.Contains("json", StringComparison.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        using JsonDocument document = JsonDocument.Parse(body);
                        result.Json = document.RootElement.Clone();
                    }
                    catch (JsonException)
                    {
                        if (isJsonMediaType)
                            await Publisher.Publish(Events.ResponseInvalidJson, $"response body is not valid JSON for {response.RequestMessage?.RequestUri}");
                    }
                }
            }

            return result;
        }
    }
}
