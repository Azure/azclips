// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

/// <summary>
/// The ACR refresh token response containing the refresh token for authentication.
/// </summary>
public class AcrRefreshToken
{
    public string? Refresh_token { get; set; }
}
