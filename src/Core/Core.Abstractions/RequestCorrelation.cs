// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Shared correlation headers for outgoing service requests.
/// </summary>
public static class RequestCorrelation
{
    public const string ClientRequestIdHeaderName = "x-ms-client-request-id";

    public static void EnsureClientRequestId(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Headers.Contains(ClientRequestIdHeaderName))
        {
            request.Headers.TryAddWithoutValidation(
                ClientRequestIdHeaderName,
                Guid.NewGuid().ToString());
        }
    }
}
