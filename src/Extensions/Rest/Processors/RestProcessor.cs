using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Cirrus.Extensions.Rest.Processors
{
    /// <summary>
    /// Universal authenticated HTTP passthrough, mirroring <c>az rest</c> (Option 2 parity): no LRO
    /// polling, no pagination, ARM audience only. Builds an arbitrary request from the supplied
    /// method/uri/payload/headers, sends it through the already-authenticated pipeline, and surfaces
    /// the raw status code, headers, and body as an untyped <see cref="Models.RestResponse"/>.
    /// </summary>
    public class RestProcessor : RestProcessorBase
    {
        private const string ArmEndpoint = "https://management.azure.com";
        private const string SubscriptionToken = "{subscriptionId}";

        /// <summary>HTTP method. Case-insensitive. Defaults to GET.</summary>
        public string Method { get; set; } = "GET";

        /// <summary>Absolute URL, or a relative ARM path (prefixed with the ARM endpoint).</summary>
        public string Uri { get; set; } = "";

        /// <summary>Optional JSON request body for POST/PUT/PATCH.</summary>
        public string Payload { get; set; } = "";

        /// <summary>Optional user headers in KEY=VALUE form.</summary>
        public List<string> Headers { get; set; } = [];

        public override async Task ProcessAsync()
        {
            EnsureRuntimeContext();
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            try
            {
                using HttpRequestMessage request = await CreateRequestMessage();
                using HttpResponseMessage response = await SendRequestAsync(request);
                Models.RestResponse result = await BuildResponseAsync(response);
                await WriteOutput(result);
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        private async Task<HttpRequestMessage> CreateRequestMessage()
        {
            if (string.IsNullOrWhiteSpace(Uri))
            {
                throw new InvalidOperationException("Uri is required.");
            }

            HttpMethod method = ResolveMethod(Method);
            string url = ResolveUrl(Uri);

            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
            var request = new HttpRequestMessage(method, url);

            // Attach the body for methods that carry one. Default the content type to
            // application/json unless the user overrides it via a Content-Type header.
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
                _ => throw new InvalidOperationException($"Unsupported HTTP method '{method}'. Supported methods: GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS.")
            };
        }

        private string ResolveUrl(string uri)
        {
            string url = uri.Trim();

            // Relative path: prefix with the ARM endpoint (host trailing slash stripped, single
            // separator), matching az rest's send_raw_request.
            if (!url.Contains("://"))
            {
                url = $"{ArmEndpoint.TrimEnd('/')}/{url.TrimStart('/')}";
            }

            if (url.Contains(SubscriptionToken, StringComparison.Ordinal))
            {
                if (AuthContext is null || string.IsNullOrWhiteSpace(AuthContext.SubscriptionId))
                {
                    throw new InvalidOperationException($"The URL contains '{SubscriptionToken}' but no subscription is available in the current auth context. Sign in first or use an explicit subscription id.");
                }

                url = url.Replace(SubscriptionToken, AuthContext.SubscriptionId, StringComparison.Ordinal);
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
                    throw new InvalidOperationException($"Invalid header '{header}'. Expected KEY=VALUE format.");
                }

                string name = header[..separator].Trim();
                string value = header[(separator + 1)..];

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

                if (!string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        using JsonDocument document = JsonDocument.Parse(body);
                        result.Json = document.RootElement.Clone();
                    }
                    catch (JsonException)
                    {
                        // Non-JSON body is valid for a passthrough; surface it as raw Content only.
                        await Publisher.Publish(Events.ResponseInvalidJson, $"response body is not valid JSON for {response.RequestMessage?.RequestUri}");
                    }
                }
            }

            return result;
        }
    }
}
