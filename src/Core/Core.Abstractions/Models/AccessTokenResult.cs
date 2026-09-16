// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security;
using System.Text.Json.Serialization;

namespace Core.Abstractions;

/// <summary>
/// The result of a token-retrieval command with the token as a plain string. Used for CLI output
/// (redacted by default unless <c>--plaintext</c>) and for PowerShell when <c>-AsPlainText</c> is
/// specified. PowerShell's default output uses <see cref="SecureAccessTokenResult"/> instead.
/// </summary>
public record AccessTokenResult
{
    /// <summary>Placeholder shown in place of the token on the CLI when <c>--plaintext</c> is not passed.</summary>
    public const string RedactedPlaceholder = "<redacted - rerun with --plaintext to reveal>";

    /// <summary>The access token. On CLI this is redacted unless <c>--plaintext</c> is passed.</summary>
    [JsonPropertyName("accessToken")]
    public required string Token { get; init; }

    /// <summary>The point in time when the access token expires.</summary>
    [JsonPropertyName("expiresOn")]
    [JsonConverter(typeof(AzureCliExpiresOnConverter))]
    public required DateTimeOffset ExpiresOn { get; init; }

    /// <summary>The access token expiry as Unix time (seconds since the epoch).</summary>
    [JsonPropertyName("expires_on")]
    public required long ExpiresOnUnixTime { get; init; }

    /// <summary>The tenant (directory) ID the token was acquired for. Empty for a managed identity account, whose profile does not carry a resolved tenant.</summary>
    [JsonPropertyName("tenant")]
    public required string TenantId { get; init; }

    /// <summary>The subscription ID associated with the request, if any.</summary>
    [JsonPropertyName("subscription")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SubscriptionId { get; init; }

    /// <summary>The user principal name of the signed-in identity, if any (null for managed identity).</summary>
    [JsonPropertyName("userId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserId { get; init; }

    /// <summary>The token type. Always "Bearer" for the flows supported today.</summary>
    [JsonPropertyName("tokenType")]
    public string Type { get; init; } = "Bearer";
}

/// <summary>
/// The result of a token-retrieval command with the token wrapped in a <see cref="SecureString"/>.
/// This is the default PowerShell output shape (matching Azure PowerShell's protected token), so the
/// raw token is not shown on screen but can still be read programmatically without re-running.
/// </summary>
public record SecureAccessTokenResult
{
    /// <summary>The access token, protected as a <see cref="SecureString"/>.</summary>
    [JsonPropertyName("accessToken")]
    public required SecureString Token { get; init; }

    /// <summary>The point in time when the access token expires.</summary>
    [JsonPropertyName("expiresOn")]
    [JsonConverter(typeof(AzureCliExpiresOnConverter))]
    public required DateTimeOffset ExpiresOn { get; init; }

    /// <summary>The access token expiry as Unix time (seconds since the epoch).</summary>
    [JsonPropertyName("expires_on")]
    public required long ExpiresOnUnixTime { get; init; }

    /// <summary>The tenant (directory) ID the token was acquired for. Empty for a managed identity account, whose profile does not carry a resolved tenant.</summary>
    [JsonPropertyName("tenant")]
    public required string TenantId { get; init; }

    /// <summary>The subscription ID associated with the request, if any.</summary>
    [JsonPropertyName("subscription")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SubscriptionId { get; init; }

    /// <summary>The user principal name of the signed-in identity, if any (null for managed identity).</summary>
    [JsonPropertyName("userId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserId { get; init; }

    /// <summary>The token type. Always "Bearer" for the flows supported today.</summary>
    [JsonPropertyName("tokenType")]
    public string Type { get; init; } = "Bearer";
}
