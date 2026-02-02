using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Client
{
    /// <summary>
    /// HTTP client for Azure Resources API operations.
    /// </summary>
    public class ResourcesClient : IDisposable
    {
        internal ActivitySource? ActivitySource { get; init; }
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly bool _shouldDispose;
        private bool _disposed;

        protected string baseUri = "https://management.azure.com";
        protected string apiVersion = "2025-04-01";

        public ResourcesClient SetBaseUri(string uri)
        {
            baseUri = uri;
            return this;
        }

        public ResourcesClient SetApiVersion(string version)
        {
            apiVersion = version;
            return this;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourcesClient"/> class with a self-contained HTTP client.
        /// </summary>
        /// <param name="jsonOptions">Optional JSON serialization options.</param>
        public ResourcesClient(JsonSerializerOptions? jsonOptions = null)
            : this(new HttpClient(), jsonOptions, shouldDispose: true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourcesClient"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use for requests.</param>
        /// <param name="jsonOptions">Optional JSON serialization options.</param>
        public ResourcesClient(HttpClient httpClient, JsonSerializerOptions? jsonOptions = null)
            : this(httpClient, jsonOptions, shouldDispose: false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourcesClient"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use for requests.</param>
        /// <param name="jsonOptions">Optional JSON serialization options.</param>
        /// <param name="shouldDispose">Whether this instance should dispose the HTTP client.</param>
        private ResourcesClient(HttpClient httpClient, JsonSerializerOptions? jsonOptions, bool shouldDispose)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _shouldDispose = shouldDispose;
            _jsonOptions = jsonOptions ?? new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        /// <summary>
        /// Sends a custom HTTP request.
        /// </summary>
        /// <param name="request">The HTTP request message.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The HTTP response message.</returns>
        public async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken = default)
        {
            using var _ = ActivitySource?.StartActivity($"{nameof(ResourcesClient)}.{nameof(SendAsync)}");
            ArgumentNullException.ThrowIfNull(request);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(@$"Request to {request.RequestUri} failed with status code {response.StatusCode}.
Response content: {await response.Content.ReadAsStringAsync()}", ex);
            }

            return response;
        }

        public async Task<ResourceGroup> CreateOrUpdateResourceGroupAsync(string subscriptionId, string resourceGroupName, ResourceGroup resourceGroup, string accessToken, CancellationToken cancellationToken = default)
        {
            using var _ = ActivitySource?.StartActivity($"{nameof(ResourcesClient)}.{nameof(CreateOrUpdateResourceGroupAsync)}");
            var requestUri = $"{baseUri}/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}?api-version={apiVersion}";

            using var request = new HttpRequestMessage(HttpMethod.Put, requestUri)
            {
                Content = JsonContent.Create(resourceGroup, options: _jsonOptions)
            };

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await SendAsync(request, cancellationToken);

            return (await response.Content.ReadFromJsonAsync<ResourceGroup>(_jsonOptions, cancellationToken))!;
        }

        public async Task<ResourceGroup> GetResourceGroupAsync(string subscriptionId, string resourceGroupName, string accessToken, CancellationToken cancellationToken = default)
        {
            using var _ = ActivitySource?.StartActivity($"{nameof(ResourcesClient)}.{nameof(GetResourceGroupAsync)}");

            var requestUri = $"{baseUri}/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}?api-version={apiVersion}";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await SendAsync(request, cancellationToken);

            return (await response.Content.ReadFromJsonAsync<ResourceGroup>(_jsonOptions, cancellationToken))!;
        }

        public async Task<IEnumerable<ResourceGroup>> ListResourceGroupsAsync(string subscriptionId, string accessToken, CancellationToken cancellationToken = default)
        {
            using var _ = ActivitySource?.StartActivity($"{nameof(ResourcesClient)}.{nameof(ListResourceGroupsAsync)}");

            var requestUri = $"{baseUri}/subscriptions/{subscriptionId}/resourcegroups?api-version={apiVersion}";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await SendAsync(request, cancellationToken);

            var result = await response.Content.ReadFromJsonAsync<ResourceGroupListResult>(_jsonOptions, cancellationToken);
            return result?.Value ?? Array.Empty<ResourceGroup>();
        }

        public async Task DeleteResourceGroupAsync(string subscriptionId, string resourceGroupName, string accessToken, CancellationToken cancellationToken = default)
        {
            using var _ = ActivitySource?.StartActivity($"{nameof(ResourcesClient)}.{nameof(DeleteResourceGroupAsync)}");

            var requestUri = $"{baseUri}/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}?api-version={apiVersion}";

            using var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            // 202 Accepted means delete is in progress, poll for completion
            if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                // Wait for deletion to complete (simplified polling)
                for (int i = 0; i < 60; i++)
                {
                    await Task.Delay(5000, cancellationToken);
                    using var checkRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);
                    checkRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                    var checkResponse = await _httpClient.SendAsync(checkRequest, cancellationToken);
                    if (checkResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return; // Successfully deleted
                    }
                }
                throw new TimeoutException($"Timed out waiting for resource group '{resourceGroupName}' to be deleted.");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return; // Already deleted
            }
            else
            {
                response.EnsureSuccessStatusCode();
            }
        }

        /// <summary>
        /// Disposes the resources used by the HTTP client.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the resources used by the HTTP client.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing && _shouldDispose)
                {
                    _httpClient?.Dispose();
                }

                _disposed = true;
            }
        }
    }
}
