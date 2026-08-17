// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Runtime;

public static class RuntimeHttpRequestOptions
{
    public static readonly HttpRequestOptionsKey<string[]> Scopes = new("Azclips.Scopes");
    public static readonly HttpRequestOptionsKey<bool> DisableRetry = new("Azclips.DisableRetry");
}
