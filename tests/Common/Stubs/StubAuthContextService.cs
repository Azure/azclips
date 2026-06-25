// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Tests.Common.Stubs;

public class StubAuthContextService : IAuthContextService
{
    private AuthContext? _context = new AuthContext(
        TenantId: "00000000-0000-0000-0000-000000000000",
        SubscriptionId: "00000000-0000-0000-0000-000000000000");

    public Task<AuthContext?> GetCurrentContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_context);
    }

    public Task SaveContextAsync(AuthContext authContext, CancellationToken cancellationToken = default)
    {
        _context = authContext;
        return Task.CompletedTask;
    }

    public Task ClearContextAsync(CancellationToken cancellationToken = default)
    {
        _context = null;
        return Task.CompletedTask;
    }
}
