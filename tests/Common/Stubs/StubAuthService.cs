// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Tests.Common.Stubs;

public class StubAuthService : IAuthService
{
    private static AuthResult CreateFakeResult() => new()
    {
        AccessToken = "stub-access-token-for-benchmark",
        ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
        Scopes = Array.Empty<string>(),
        TenantId = "00000000-0000-0000-0000-000000000000",
        CorrelationId = Guid.Empty
    };

    public Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateFakeResult());
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<AuthResult> AcquireTokenSilentlyAsync(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateFakeResult());
    }
}
