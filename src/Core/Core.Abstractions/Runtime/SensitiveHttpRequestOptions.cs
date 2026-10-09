// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;

namespace Core.Abstractions.Runtime;

public static class SensitiveHttpRequestOptions
{
    private static readonly HttpRequestOptionsKey<bool> SensitiveRequest = new("Azclips.SensitiveHttpRequest");

    public static void MarkSensitive(this HttpRequestMessage request) => request.Options.Set(SensitiveRequest, true);

    public static bool HasSensitiveDiagnostics(this HttpRequestMessage? request) =>
        request is not null && request.Options.TryGetValue(SensitiveRequest, out bool sensitive) && sensitive;

    public static string FailureMessage(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => "Authentication failed. Check the profile, tenant and token audience.",
        HttpStatusCode.Forbidden => "Access denied. Check permissions and network access rules.",
        _ => $"Request failed with HTTP {(int)statusCode}. Response details are omitted because the request contains sensitive data."
    };
}
