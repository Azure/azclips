// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
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

    /// <summary>
    /// Runs the base's <c>BeforeProcessAsync</c> pipeline stage — which fires the
    /// <c>OverrideBeforeProcessBase</c> partial method that lives under
    /// <c>Customization/GraphProcessorBase.cs</c> — so tests can observe the auth-override
    /// installation without setting up an HTTP pipeline for a full <c>ProcessAsync</c> run.
    /// </summary>
    private static async Task InvokeBeforeProcessAsync(GraphProcessorBase processor)
    {
        MethodInfo method = processor.GetType().GetMethod(
            "BeforeProcessAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("BeforeProcessAsync not found on processor.");
        await (Task)method.Invoke(processor, null)!;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("sub")]
    public async Task BeforeProcessAsync_ShouldRouteTokenAcquisitionToGraphScope(string? subscriptionId)
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
            Profile = new Profile { TenantId = "tenant", SubscriptionId = subscriptionId },
            AcquireTokenAsyncDefault = engineDefault,
            ApplyOverride = false
        };

        // Init captures the auth handler; BeforeProcessAsync installs the Graph scope override
        // (via the OverrideBeforeProcessBase partial method in Customization/).
        var processor = new GetUserProcessor();
        processor.Init(CreateContext(authHandler));
        await InvokeBeforeProcessAsync(processor);

        Assert.True(authHandler.ApplyOverride);
        Assert.NotNull(authHandler.AcquireTokenAsyncOverride);

        // Invoke the override the way the pipeline would (passing null scopes); it must force Graph scope.
        await authHandler.AcquireTokenAsyncOverride!(null, "tenant", false, CancellationToken.None, null);

        Assert.NotNull(capturedScopes);
        Assert.Equal(["https://graph.microsoft.com/.default"], capturedScopes);
    }

    [Fact]
    public async Task BeforeProcessAsync_ShouldPreserveTenantAndPromptWhenForcingGraphScope()
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

        var processor = new GetUserProcessor();
        processor.Init(CreateContext(authHandler));
        await InvokeBeforeProcessAsync(processor);

        await authHandler.AcquireTokenAsyncOverride!(null, "contoso-tenant", true, CancellationToken.None, null);

        // Only the scope is forced; tenant and promptOnError pass through unchanged.
        Assert.Equal("contoso-tenant", capturedTenant);
        Assert.True(capturedPrompt);
    }
}
