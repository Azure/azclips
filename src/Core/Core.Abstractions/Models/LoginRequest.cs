// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public class LoginRequest
{
    public string? TenantId { get; init; }
    public string? SubscriptionId { get; init; }

    public string[]? Scopes { get; init; }
    public bool UseDeviceCodeFlow { get; init; } = false;
}
