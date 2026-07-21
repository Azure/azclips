// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

/// <summary>
/// OAuth2 token response from ACR token exchange endpoint.
/// </summary>
internal class AcrTokenResponse
{
    public string? Access_token { get; set; }
    public string? Refresh_token { get; set; }
}
