namespace Microsoft.Azure.Cirrus.Resources.Client
{
    using System.Net.Http.Json;
    using System.Text.Json;

    /// <summary>
    /// HTTP client for Azure Resources API operations.
    /// </summary>
    public class ResourcesClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly bool _shouldDispose;
        private bool _disposed;

        // remove when integrate with auth library
        protected string accessToken;
        protected string baseUri = "https://management.azure.com/";
        protected string apiVersion = "2025-04-01";


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
            ArgumentNullException.ThrowIfNull(request);

            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return response;
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