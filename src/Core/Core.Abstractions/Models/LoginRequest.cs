// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public sealed record LoginRequest(
    string? TenantId,
    string? SubscriptionId,
    string[]? Scopes,
    AuthCredential Credential);
