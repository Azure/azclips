// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Agent.Providers;
using Tests.Common.Stubs;
using Xunit;

namespace Core.Agent.Tests;

public class BuiltinEndpointResolverTests
{
    private const string EastUsHost = "azclis-copilot-apim-ppe-eus.azure-api.net";
    private const string WestEuropeHost = "azclis-copilot-apim-ppe-weu.azure-api.net";

    [Fact]
    public async Task ConfiguredEndpoint_IsUsedWithoutBoundaryLookup()
    {
        var lookups = 0;
        var options = new BuiltinProviderOptions { Endpoint = "https://gateway.example/azclips/openai/" };

        var endpoint = await BuiltinEndpointResolver.ResolveAsync(
            options,
            _ => { lookups++; return Task.FromResult<string?>("EU"); },
            CancellationToken.None);

        Assert.Equal(options.Endpoint, endpoint.Url);
        Assert.Equal(BuiltinEndpointResolver.ConfiguredSource, endpoint.Source);
        Assert.Null(endpoint.Boundary);
        Assert.Equal("gateway.example", endpoint.Host);
        Assert.Equal(0, lookups);
    }

    [Theory]
    [InlineData("Global")]
    [InlineData("global")]
    [InlineData("GLOBAL")]
    public async Task DefaultOptions_ConfirmedGlobalBoundary_UsesEastUs(string boundary)
    {
        var lookups = 0;

        var endpoint = await BuiltinEndpointResolver.ResolveAsync(
            new BuiltinProviderOptions(),
            _ => { lookups++; return Task.FromResult<string?>(boundary); },
            CancellationToken.None);

        Assert.Equal(EastUsHost, endpoint.Host);
        Assert.Equal(BuiltinEndpointResolver.BoundarySource, endpoint.Source);
        Assert.Equal(boundary, endpoint.Boundary);
        Assert.Equal(1, lookups);
    }

    [Theory]
    [InlineData("EU", "EU")]
    [InlineData("Unknown", "Unknown")]
    [InlineData("SomethingElse", "SomethingElse")]
    [InlineData(null, "Unknown")]
    [InlineData("", "Unknown")]
    [InlineData("   ", "Unknown")]
    public async Task UnconfirmedBoundary_UsesWestEurope(string? boundary, string recorded)
    {
        var endpoint = await BuiltinEndpointResolver.ResolveAsync(
            new BuiltinProviderOptions(),
            _ => Task.FromResult(boundary),
            CancellationToken.None);

        Assert.Equal(WestEuropeHost, endpoint.Host);
        Assert.Equal(BuiltinEndpointResolver.BoundarySource, endpoint.Source);
        Assert.Equal(recorded, endpoint.Boundary);
    }

    [Fact]
    public async Task WhitespaceEndpoint_ChoosesByBoundary()
    {
        var endpoint = await BuiltinEndpointResolver.ResolveAsync(
            new BuiltinProviderOptions { Endpoint = "   " },
            _ => Task.FromResult<string?>("Global"),
            CancellationToken.None);

        Assert.Equal(EastUsHost, endpoint.Host);
        Assert.Equal(BuiltinEndpointResolver.BoundarySource, endpoint.Source);
    }

    [Fact]
    public async Task BoundaryLookupFailure_UsesWestEurope()
    {
        var endpoint = await BuiltinEndpointResolver.ResolveAsync(
            new BuiltinProviderOptions(),
            _ => Task.FromException<string?>(new InvalidOperationException("lookup failed")),
            CancellationToken.None);

        Assert.Equal(WestEuropeHost, endpoint.Host);
        Assert.Equal("Unknown", endpoint.Boundary);
    }

    [Fact]
    public async Task Cancellation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BuiltinEndpointResolver.ResolveAsync(
            new BuiltinProviderOptions(),
            token => Task.FromCanceled<string?>(token),
            cancellation.Token));
    }

    [Fact]
    public async Task CancelledLookupReturningNull_Throws()
    {
        // TelemetryRoutingResolver swallows caller cancellation and returns null.
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BuiltinEndpointResolver.ResolveAsync(
            new BuiltinProviderOptions(),
            _ => { cancellation.Cancel(); return Task.FromResult<string?>(null); },
            cancellation.Token));
    }

    [Fact]
    public async Task ManagedIdentityProfile_UsesWestEurope()
    {
        // Managed-identity profiles carry no tenant, so no boundary can be confirmed.
        var profiles = new StubProfileService(seedDefault: false);
        await profiles.SaveAndActivateProfileAsync(new Profile
        {
            Name = "default",
            TenantId = "",
            SubscriptionId = "sub-1",
            CloudName = "AzureCloud",
            AuthMethod = AuthMethod.ManagedIdentity,
        });

        var endpoint = await BuiltinEndpointResolver.ResolveAsync(
            new BuiltinProviderOptions(), profiles, new StubAuthService(), CancellationToken.None);

        Assert.Equal(WestEuropeHost, endpoint.Host);
        Assert.Equal(BuiltinEndpointResolver.BoundarySource, endpoint.Source);
        Assert.Equal("Unknown", endpoint.Boundary);
    }

    [Theory]
    [InlineData("Global", EastUsHost, "Global")]
    [InlineData("EU", WestEuropeHost, "EU")]
    [InlineData(null, WestEuropeHost, "Unknown")]
    public void Choose_UsesTheBoundaryTheHostAlreadyResolved(string? boundary, string host, string recorded)
    {
        var endpoint = BuiltinEndpointResolver.Choose(new BuiltinProviderOptions(), boundary);

        Assert.Equal(host, endpoint.Host);
        Assert.Equal(BuiltinEndpointResolver.BoundarySource, endpoint.Source);
        Assert.Equal(recorded, endpoint.Boundary);
    }

    [Fact]
    public void Choose_ConfiguredEndpoint_IgnoresTheBoundary()
    {
        var options = new BuiltinProviderOptions { Endpoint = "https://gateway.example/azclips/openai/" };

        var endpoint = BuiltinEndpointResolver.Choose(options, "Global");

        Assert.Equal(options.Endpoint, endpoint.Url);
        Assert.Equal(BuiltinEndpointResolver.ConfiguredSource, endpoint.Source);
        Assert.Null(endpoint.Boundary);
    }
}
