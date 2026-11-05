// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Net;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Authentication;
using Core.Runtime;
using Core.Runtime.Handler;
using Moq;

namespace Cli.Tests;

public class HttpPipelineFactoryTests
{
    [Fact]
    public async Task CredentialsReuseTheAuthServiceWithoutEagerAcquisitionOrScopeMutation()
    {
        var profile = new Profile { Name = "source", TenantId = "tenant" };
        using var cancellation = new CancellationTokenSource();
        int acquisitions = 0;
        var service = Service((scopes, tenant, prompt, token, selected) =>
        {
            Assert.Same(profile, selected);
            Assert.Equal(profile.TenantId, tenant);
            Assert.False(prompt);
            Assert.Equal(cancellation.Token, token);
            string scope = Assert.Single(scopes!);
            scopes![0] = "changed-by-auth-service";
            acquisitions++;
            return Task.FromResult(Result(scope, [scope]));
        });
        var credential = service.CreateCredential(profile);
        Assert.Same(profile, credential.Profile);
        Assert.Equal(0, acquisitions);
        string[] storageScopes = ["storage"];
        var storage = await credential.GetTokenAsync(storageScopes, cancellation.Token);
        var arm = await credential.GetTokenAsync(["arm"], cancellation.Token);
        Assert.Equal(["storage"], storageScopes);
        Assert.Equal("storage", storage.AccessToken);
        Assert.Equal("arm", arm.AccessToken);
        Assert.Equal(2, acquisitions);
    }

    [Fact]
    public async Task ConcurrentPipelinesUseIndependentProfilesScopesAndCancellation()
    {
        var source = new Profile { Name = "source", TenantId = "source-tenant" };
        var destination = new Profile { Name = "destination", TenantId = "destination-tenant" };
        using var cancellation = new CancellationTokenSource();
        var publisher = new Mock<IEventPublisher>();
        publisher.SetupGet(value => value.Token).Returns(cancellation.Token);
        var bothAcquiring = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int acquisitions = 0;
        var service = Service(async (scopes, tenant, _, token, profile) =>
        {
            Assert.Equal(cancellation.Token, token);
            Assert.Equal(profile!.TenantId, tenant);
            Assert.Equal(profile == source ? new[] { "storage-scope" } : new[] { "arm-scope" }, scopes);
            if (Interlocked.Increment(ref acquisitions) == 2) bothAcquiring.SetResult();
            await release.Task.WaitAsync(token);
            return Result(profile.Name, scopes!, profile.TenantId);
        });
        var factory = CreateFactory(service, request =>
        {
            Assert.DoesNotContain(request.Options, option => option.Key == "Azclips.RequestAuthentication");
            string expected = request.RequestUri!.Host.StartsWith("source", StringComparison.Ordinal) ? "source" : "destination";
            Assert.Equal(expected, request.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        string[] sourceScopes = ["storage-scope"];
        var sourcePipeline = factory.Create(source, sourceScopes);
        var destinationPipeline = factory.Create(destination, ["arm-scope"]);
        sourceScopes[0] = "changed-after-binding";
        Assert.NotSame(sourcePipeline, destinationPipeline);
        using var sourceRequest = new HttpRequestMessage(HttpMethod.Get, "https://source.blob.core.windows.net/container/blob");
        using var destinationRequest = new HttpRequestMessage(HttpMethod.Post, "https://management.azure.com/");
        var first = sourcePipeline.SendAsync(sourceRequest, publisher.Object);
        var second = destinationPipeline.SendAsync(destinationRequest, publisher.Object);
        try
        {
            await bothAcquiring.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            release.TrySetResult();
        }
        var responses = await Task.WhenAll(first, second);
        foreach (var response in responses) response.Dispose();
        Assert.Equal(2, acquisitions);
    }

    [Fact]
    public async Task OneCredentialCanServeParallelRequestsAndDifferentAudiences()
    {
        var profile = new Profile { Name = "source", TenantId = "tenant" };
        var calls = new ConcurrentQueue<string>();
        var service = Service(async (scopes, _, _, _, selected) =>
        {
            Assert.Same(profile, selected);
            string scope = Assert.Single(scopes!);
            calls.Enqueue(scope);
            scopes![0] = "mutated-by-token-acquirer";
            await Task.Yield();
            return Result(scope, [scope]);
        });
        var credential = service.CreateCredential(profile);
        var factory = CreateFactory(service, _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var storage = factory.Create(credential, ["storage"]);
        var arm = factory.Create(credential, ["arm"]);
        var publisher = Mock.Of<IEventPublisher>();
        using var first = new HttpRequestMessage(HttpMethod.Get, "https://fixture.blob.core.windows.net/one");
        using var second = new HttpRequestMessage(HttpMethod.Get, "https://fixture.blob.core.windows.net/two");
        using var third = new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/");
        var responses = await Task.WhenAll(
            storage.SendAsync(first, publisher), storage.SendAsync(second, publisher), arm.SendAsync(third, publisher));
        foreach (var response in responses) response.Dispose();
        Assert.Equal("storage", first.Headers.Authorization?.Parameter);
        Assert.Equal("storage", second.Headers.Authorization?.Parameter);
        Assert.Equal("arm", third.Headers.Authorization?.Parameter);
        Assert.Equal(2, calls.Count(scope => scope == "storage"));
        Assert.Equal(1, calls.Count(scope => scope == "arm"));
    }

    [Fact]
    public async Task CancellationInterruptsTokenAcquisitionBeforeTransport()
    {
        using var cancellation = new CancellationTokenSource();
        var acquiring = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publisher = new Mock<IEventPublisher>();
        publisher.SetupGet(value => value.Token).Returns(cancellation.Token);
        var service = Service(async (_, _, _, token, _) =>
        {
            acquiring.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("Cancellation should stop token acquisition.");
        });
        var factory = CreateFactory(service, _ => throw new InvalidOperationException("A cancelled request must not be sent."));
        var pipeline = factory.Create(new Profile { TenantId = "tenant" }, ["storage"]);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://fixture.blob.core.windows.net/");
        var sending = pipeline.SendAsync(request, publisher.Object);
        await acquiring.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sending);
    }

    [Fact]
    public async Task CustomCredentialsCannotChangePipelineScopeBindings()
    {
        var credential = new Mock<IProfileCredential>();
        credential.SetupGet(value => value.Profile).Returns(new Profile());
        int acquisitions = 0;
        credential.Setup(value => value.GetTokenAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .Returns((string[] scopes, CancellationToken _) =>
            {
                Assert.Equal(["storage"], scopes);
                scopes[0] = "changed-by-custom-credential";
                acquisitions++;
                return Task.FromResult(Result("custom-token", ["storage"]));
            });
        var factory = CreateFactory(Mock.Of<IAuthService>(), request =>
        {
            Assert.Equal("custom-token", request.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        string[] scopes = ["storage"];
        var pipeline = factory.Create(credential.Object, scopes);
        scopes[0] = "changed-by-caller";
        for (int index = 0; index < 2; index++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://fixture.blob.core.windows.net/");
            using var response = await pipeline.SendAsync(request, Mock.Of<IEventPublisher>());
        }
        Assert.Equal(2, acquisitions);
    }

    [Fact]
    public async Task CancellationIsRespectedEvenIfACredentialReturnsATokenAfterCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var publisher = new Mock<IEventPublisher>();
        publisher.SetupGet(value => value.Token).Returns(cancellation.Token);
        var service = Service((_, _, _, _, _) =>
        {
            cancellation.Cancel();
            return Task.FromResult(Result("token", ["storage"]));
        });
        var factory = CreateFactory(service, _ => throw new InvalidOperationException("Transport must not be called."));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://fixture.blob.core.windows.net/");
        var pipeline = factory.Create(new Profile(), ["storage"]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pipeline.SendAsync(request, publisher.Object));
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task TokenAcquisitionFailuresPropagateWithoutSending()
    {
        var failure = new InvalidOperationException("Fixture authentication failure.");
        var service = Service((_, _, _, _, _) => throw failure);
        var factory = CreateFactory(service, _ => throw new InvalidOperationException("Transport must not be called."));
        var pipeline = factory.Create(new Profile { TenantId = "tenant" }, ["storage"]);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://fixture.blob.core.windows.net/");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.SendAsync(request, Mock.Of<IEventPublisher>()));
        Assert.Same(failure, actual);
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task AuthenticationEventsEnrichMetadataWithoutMutatingTheCredential()
    {
        var profile = new Profile { Name = "source", TenantId = "tenant" };
        var service = Service((_, _, _, _, _) => Task.FromResult(Result("token", ["storage"])));
        var credential = service.CreateCredential(profile);
        var publisher = new Mock<IEventPublisher>();
        Profile? enriched = null;
        publisher.Setup(value => value.Publish(Events.AuthAfterTokenAcquisition, It.IsAny<EventData>()))
            .Callback<string, EventData>((_, data) => enriched = Assert.IsType<Profile>(data.Payload))
            .Returns(Task.CompletedTask);
        var factory = CreateFactory(service, _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://fixture.blob.core.windows.net/");
        using var response = await factory.Create(credential, ["storage"]).SendAsync(request, publisher.Object);
        publisher.Verify(value => value.Publish(Events.AuthBeforeTokenAcquisition), Times.Once);
        Assert.Equal("fixture-object", Assert.IsType<Profile>(enriched).Metadata.EntraObjectId);
        Assert.Same(profile, credential.Profile);
        Assert.Null(profile.Metadata.EntraObjectId);
    }

    [Fact]
    public async Task FactoryRetainsThePipelineRecipeAndRetryBehavior()
    {
        var service = Service((_, _, _, _, _) => Task.FromResult(Result("token", ["storage"])));
        int attempts = 0;
        var factory = new HttpPipelineFactory(service.CreateCredential, authentication =>
            new HttpPipeline(new Terminal(request =>
            {
                int attempt = Interlocked.Increment(ref attempts);
                if (attempt == 1)
                    Assert.Equal("fixture-agent", Assert.Single(request.Headers.GetValues("User-Agent")));
                Assert.True(request.Headers.Contains(RequestCorrelation.ClientRequestIdHeaderName));
                Assert.Equal("token", request.Headers.Authorization?.Parameter);
                return Task.FromResult(new HttpResponseMessage(
                    attempt == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
            }), maxRetry: 1, maxRetryFor429: 1)
            .Prepend(authentication)
            .Prepend(new ClientRequestIdHandler().SendAsync)
            .Prepend((request, publisher, next) =>
            {
                request.Headers.Add("User-Agent", "fixture-agent");
                return next.SendAsync(request, publisher);
            }));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://fixture.blob.core.windows.net/");
        using var response = await factory.Create(new Profile(), ["storage"]).SendAsync(request, Mock.Of<IEventPublisher>());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, attempts);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void InvalidScopeElementsAreRejected(string? scope)
    {
        var factory = CreateFactory(Mock.Of<IAuthService>(), _ => throw new InvalidOperationException());
        Assert.ThrowsAny<ArgumentException>(() => factory.Create(Mock.Of<IProfileCredential>(), [scope!]));
    }

    [Fact]
    public void EmptyAndNullScopesAreRejected()
    {
        var factory = CreateFactory(Mock.Of<IAuthService>(), _ => throw new InvalidOperationException());
        var credential = Mock.Of<IProfileCredential>();
        Assert.Throws<ArgumentException>(() => factory.Create(credential, []));
        Assert.Throws<ArgumentNullException>(() => factory.Create(credential, null!));
    }

    [Fact]
    public async Task PreCancelledCredentialDoesNotCallTheAuthService()
    {
        var service = new Mock<IAuthService>(MockBehavior.Strict);
        var credential = service.Object.CreateCredential(new Profile());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => credential.GetTokenAsync(["storage"], cancellation.Token));
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultContextsKeepExistingOverrideBehavior(bool applyOverride)
    {
        var profile = new Profile { TenantId = "tenant" };
        int defaultCalls = 0, overrideCalls = 0;
        var auth = new AuthHandler
        {
            Profile = profile, ApplyOverride = applyOverride,
            AcquireTokenAsyncDefault = (scopes, tenant, _, token, selectedProfile) =>
            {
                Assert.Null(scopes);
                Assert.Equal(profile.TenantId, tenant);
                Assert.Same(profile, selectedProfile);
                Assert.Equal(CancellationToken.None, token);
                defaultCalls++;
                return Task.FromResult(Result("default", []));
            },
            AcquireTokenAsyncOverride = (_, _, _, _, _) =>
            {
                overrideCalls++;
                return Task.FromResult(Result("override", []));
            }
        };
        var terminal = new Terminal(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/");
        using var response = await auth.SendAsync(request, Mock.Of<IEventPublisher>(), terminal);
        Assert.Equal(applyOverride ? 0 : 1, defaultCalls);
        Assert.Equal(applyOverride ? 1 : 0, overrideCalls);
        Assert.Equal(applyOverride ? "override" : "default", request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task DefaultContextWithoutAProfileStillSkipsAuthentication()
    {
        var auth = new AuthHandler
        {
            AcquireTokenAsyncDefault = (_, _, _, _, _) => throw new InvalidOperationException("Token acquisition must not occur.")
        };
        var terminal = new Terminal(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/");
        using var response = await auth.SendAsync(request, Mock.Of<IEventPublisher>(), terminal);
        Assert.Null(request.Headers.Authorization);
    }

    private static IAuthService Service(AcquireTokenAsyncDelegate acquire)
    {
        var service = new Mock<IAuthService>();
        service.Setup(value => value.AcquireTokenSilentlyAsync(
            It.IsAny<string[]?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<Profile?>()))
            .Returns((string[]? scopes, string? tenant, bool prompt, CancellationToken token, Profile? profile) =>
                acquire(scopes, tenant, prompt, token, profile));
        return service.Object;
    }

    private static HttpPipelineFactory CreateFactory(IAuthService service, Func<HttpRequestMessage, Task<HttpResponseMessage>> send) =>
        new(service.CreateCredential, authentication =>
            new HttpPipeline(new Terminal(send), maxRetry: 1, maxRetryFor429: 1).Prepend(authentication));

    private sealed class Terminal(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : ISendAsyncTerminalFactory, ISendAsync
    {
        public ISendAsync Create() => this;
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventPublisher callback) => send(request);
    }

    private static AuthResult Result(string token, string[] scopes, string tenant = "tenant") => new()
    {
        AccessToken = token, Scopes = scopes, TenantId = tenant,
        ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(5), EntraObjectId = "fixture-object"
    };
}
