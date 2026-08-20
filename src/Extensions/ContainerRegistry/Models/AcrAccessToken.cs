// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

/// <summary>
/// The ACR access token response containing the access token for authentication.
/// </summary>
public class AcrAccessToken
{
    public string? Access_token { get; set; }
}
