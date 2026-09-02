// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Runtime;

public static class RuntimeHttpRequestOptions
{
    public static readonly HttpRequestOptionsKey<string[]> Scopes = new("Azclips.Scopes");
    public static readonly HttpRequestOptionsKey<bool> DisableProfileAuthentication = new("Azclips.DisableProfileAuthentication");
    public static readonly HttpRequestOptionsKey<bool> DisableRetry = new("Azclips.DisableRetry");
    public static readonly HttpRequestOptionsKey<bool> DisableBodyLogging = new("Azclips.DisableBodyLogging");
    public static readonly HttpRequestOptionsKey<bool> DisableQueryLogging = new("Azclips.DisableQueryLogging");
}
