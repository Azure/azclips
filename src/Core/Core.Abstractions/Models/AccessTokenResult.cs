// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// The result of a token-retrieval command (<c>az account get-access-token</c> /
/// <c>Get-AzAccessToken</c>). Carries the acquired access token together with its
/// non-secret metadata.
/// </summary>
/// <remarks>
/// The <see cref="Token"/> value is a secret. Command output is secure by default:
/// front ends MUST NOT render the raw token unless the caller has explicitly opted into
/// plaintext (CLI <c>--plaintext</c> flag; PowerShell
/// <c>AZUREPS_OUTPUT_PLAINTEXT_AZACCESSTOKEN</c> environment variable). The
/// <see cref="RedactedPlaceholder"/> is the value shown in the token field when plaintext
/// has not been requested.
/// </remarks>
public record AccessTokenResult
{
    /// <summary>The placeholder shown in place of the token when plaintext output is not requested.</summary>
    public const string RedactedPlaceholder = "<redacted - rerun with --plaintext to reveal>";

    /// <summary>The raw access token. Treated as a secret; not surfaced in default output.</summary>
    public required string Token { get; init; }

    /// <summary>The point in time when the access token expires.</summary>
    public required DateTimeOffset ExpiresOn { get; init; }

    /// <summary>The tenant (directory) ID the token was acquired for.</summary>
    public required string TenantId { get; init; }

    /// <summary>The subscription ID associated with the request, if any.</summary>
    public string? SubscriptionId { get; init; }

    /// <summary>The token type. Always "Bearer" for the flows supported today.</summary>
    public string TokenType { get; init; } = "Bearer";
}
