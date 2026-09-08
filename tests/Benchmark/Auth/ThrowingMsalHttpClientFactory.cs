// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Identity.Client;

namespace Benchmark.Auth;

/// <summary>
/// Benchmark MSAL HttpClientFactory to ensure no network calls are made during a benchmark.
/// </summary>
internal sealed class ThrowingMsalHttpClientFactory : IMsalHttpClientFactory
{
    public HttpClient GetHttpClient() => new(new ThrowingHandler());

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                $"MSAL attempted a network request to '{request.RequestUri}' during a benchmark. " +
                "Benchmarks must resolve tokens from the seeded cache; a network call would " +
                "contaminate the measurement. Check the seed fixture and the stub profile's UPN.");
    }
}
