// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Runtime.Handler;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;
using Moq;

namespace Extensions.MsGraph.Tests;

public class GraphProcessorBaseAuthScopeTests
{
    private static ProcessorContext CreateContext(AuthHandler authHandler) =>
        new(
            HttpPipeline: null!,
            Publisher: Mock.Of<IEventPublisher>(),
            AuthHandler: authHandler,
            OutputHandler: new OutputHandler { WriteOutput = (_, _) => Task.CompletedTask });

    [Fact]
    public async Task Init_ShouldRouteTokenAcquisitionToGraphScope()
    {
        string[]? capturedScopes = null;
        AcquireTokenAsyncDelegate engineDefault = (scopes, tenantId, prompt, ct, profile) =>
        {
            capturedScopes = scopes;
            return Task.FromResult(new AuthResult
            {
                AccessToken = "token",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                Scopes = scopes ?? [],
                TenantId = tenantId ?? "",
                CorrelationId = Guid.Empty
            });
        };

        var authHandler = new AuthHandler
        {
            Profile = new Profile { TenantId = "tenant", SubscriptionId = "sub" },
            AcquireTokenAsyncDefault = engineDefault,
            ApplyOverride = false
        };

        // Init installs the Graph scope override.
        new GetUserProcessor().Init(CreateContext(authHandler));

        Assert.True(authHandler.ApplyOverride);
        Assert.NotNull(authHandler.AcquireTokenAsyncOverride);

        // Invoke the override the way the pipeline would (passing null scopes); it must force Graph scope.
        await authHandler.AcquireTokenAsyncOverride!(null, "tenant", false, CancellationToken.None, null);

        Assert.Equal(["https://graph.microsoft.com/.default"], capturedScopes);
    }

    [Fact]
    public async Task Init_ShouldPreserveTenantAndPromptWhenForcingGraphScope()
    {
        string? capturedTenant = null;
        bool capturedPrompt = false;
        AcquireTokenAsyncDelegate engineDefault = (scopes, tenantId, prompt, ct, profile) =>
        {
            capturedTenant = tenantId;
            capturedPrompt = prompt;
            return Task.FromResult(new AuthResult
            {
                AccessToken = "token",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                Scopes = scopes ?? [],
                TenantId = tenantId ?? "",
                CorrelationId = Guid.Empty
            });
        };

        var authHandler = new AuthHandler
        {
            Profile = new Profile { TenantId = "tenant", SubscriptionId = "sub" },
            AcquireTokenAsyncDefault = engineDefault
        };

        new GetUserProcessor().Init(CreateContext(authHandler));
        await authHandler.AcquireTokenAsyncOverride!(null, "contoso-tenant", true, CancellationToken.None, null);

        // Only the scope is forced; tenant and promptOnError pass through unchanged.
        Assert.Equal("contoso-tenant", capturedTenant);
        Assert.True(capturedPrompt);
    }
}
