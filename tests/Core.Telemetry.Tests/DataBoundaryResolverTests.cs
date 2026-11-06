// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Runtime;
using Core.Runtime.Persistence;
using Tests.Common.Stubs;

namespace Core.Telemetry.Tests;

[CollectionDefinition(nameof(DataBoundaryResolverTests), DisableParallelization = true)]
public sealed class DataBoundaryResolverTestsCollection { }

[Collection(nameof(DataBoundaryResolverTests))]
public class DataBoundaryResolverTests
{
    [Theory]
    [InlineData(null, false, null, UploadRegion.ROW)]
    [InlineData("tenant-1", false, null, UploadRegion.ROW)]
    [InlineData("tenant-1", true, "EU", UploadRegion.EU)]
    [InlineData("tenant-1", true, "Global", UploadRegion.ROW)]
    [InlineData("tenant-1", true, null, UploadRegion.EU)]
    [InlineData("tenant-1", true, "APAC", UploadRegion.EU)]
    public async Task ResolveUploadRegionAsync_AppliesPolicyAndCachesOnlyConfirmedBoundaries(
        string? tenant, bool authenticated, string? boundary, UploadRegion expected)
    {
        using var home = new TestHome();
        var lookupCalls = 0;
        var region = await home.Resolver.ResolveUploadRegionAsync(tenant,
            _ => Task.FromResult(authenticated ? JwtForTenant(tenant!) : null),
            (_, _) => { lookupCalls++; return Task.FromResult(boundary); },
            TimeSpan.FromSeconds(1));

        Assert.Equal(expected, region);
        Assert.Equal(authenticated ? 1 : 0, lookupCalls);
        if (tenant is not null)
            Assert.Equal(boundary is "EU" or "Global" ? boundary : null, await home.Cache.GetAsync(tenant));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ResolveAsync_NoTenant_SkipsProviders(string? tenant)
    {
        using var home = new TestHome();
        var result = await home.Resolver.ResolveAsync(tenant,
            _ => throw new InvalidOperationException("Token must not be requested."),
            (_, _) => throw new InvalidOperationException("Boundary must not be requested."),
            TimeSpan.FromSeconds(1));
        Assert.Equal(DataBoundaryOutcome.TenantUnavailable, result.Outcome);
        Assert.Null(result.Boundary);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-token")]
    public async Task ResolveAsync_InvalidToken_IsAuthenticationUnavailable(string? token)
    {
        using var home = new TestHome();
        var result = await home.Resolver.ResolveAsync("tenant-1",
            _ => Task.FromResult(token),
            (_, _) => throw new InvalidOperationException("Boundary must not be requested."),
            TimeSpan.FromSeconds(1));
        Assert.Equal(DataBoundaryOutcome.AuthenticationUnavailable, result.Outcome);
        Assert.Null(result.Boundary);
        Assert.Null(await home.Cache.GetAsync("tenant-1"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResolveAsync_TokenFailureOrTimeout_IsAuthenticationUnavailable(bool timeout)
    {
        using var home = new TestHome();
        var result = await home.Resolver.ResolveAsync("tenant-1",
            async ct =>
            {
                if (timeout)
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                throw new InvalidOperationException("Authentication failed.");
            },
            (_, _) => throw new InvalidOperationException("Boundary must not be requested."),
            TimeSpan.FromMilliseconds(20));
        Assert.Equal(DataBoundaryOutcome.AuthenticationUnavailable, result.Outcome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Unknown")]
    [InlineData("APAC")]
    public async Task ResolveAsync_UnconfirmedResponse_IsBoundaryUnavailableAndNotCached(string? boundary)
    {
        using var home = new TestHome();
        var result = await home.Resolver.ResolveAsync("tenant-1",
            _ => Task.FromResult<string?>(JwtForTenant("tenant-1")),
            (_, _) => Task.FromResult(boundary),
            TimeSpan.FromSeconds(1));
        Assert.Equal(DataBoundaryOutcome.BoundaryUnavailable, result.Outcome);
        Assert.Null(result.Boundary);
        Assert.Null(await home.Cache.GetAsync("tenant-1"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResolveAsync_BoundaryFailureOrTimeout_UsesSameOutcome(bool timeout)
    {
        using var home = new TestHome();
        var result = await home.Resolver.ResolveAsync("tenant-1",
            _ => Task.FromResult<string?>(JwtForTenant("tenant-1")),
            async (_, ct) =>
            {
                if (timeout)
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                throw new HttpRequestException("Boundary request failed.");
            },
            TimeSpan.FromMilliseconds(20));
        Assert.Equal(DataBoundaryOutcome.BoundaryUnavailable, result.Outcome);
        Assert.Null(result.Boundary);
        Assert.Null(await home.Cache.GetAsync("tenant-1"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResolveAsync_CallerCancellation_Propagates(bool duringBoundary)
    {
        using var home = new TestHome();
        using var cts = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            home.Resolver.ResolveAsync("tenant-1",
                _ =>
                {
                    if (!duringBoundary) cts.Cancel();
                    return Task.FromResult<string?>(JwtForTenant("tenant-1"));
                },
                (_, _) =>
                {
                    cts.Cancel();
                    return Task.FromResult<string?>("EU");
                },
                TimeSpan.FromSeconds(1), cts.Token));
        Assert.Null(await home.Cache.GetAsync("tenant-1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Unknown")]
    [InlineData("APAC")]
    public async Task ResolveAsync_InvalidCachedBoundary_IsRefreshed(string cached)
    {
        using var home = new TestHome();
        WriteCache(home.Path, new Dictionary<string, string> { ["tenant-1"] = cached });
        var called = false;
        var result = await home.Resolver.ResolveAsync("tenant-1",
            _ => Task.FromResult<string?>(JwtForTenant("tenant-1")),
            (_, _) => { called = true; return Task.FromResult<string?>("global"); },
            TimeSpan.FromSeconds(1));
        Assert.True(called);
        Assert.Equal(new DataBoundaryResolution("Global", DataBoundaryOutcome.Confirmed), result);
        Assert.Equal("Global", await home.Cache.GetAsync("tenant-1"));
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("APAC")]
    public async Task Cache_RejectsUnconfirmedBoundaries(string boundary)
    {
        using var home = new TestHome();
        await Assert.ThrowsAsync<ArgumentException>(() => home.Cache.SetAsync("tenant-1", boundary));
        Assert.Null(await home.Cache.GetAsync("tenant-1"));
    }

    [Fact]
    public async Task ResolveAsync_ReturnsCachedBoundary_WithoutCallingProviders()
    {
        using var home = new TestHome();
        WriteCache(home.Path, new Dictionary<string, string> { ["tenant-1"] = "EU" });
        var tokenCalled = false;
        var boundaryCalled = false;

        var resolution = await home.Resolver.ResolveAsync(
            "tenant-1",
            _ => { tokenCalled = true; return Task.FromResult<string?>(JwtForTenant("tenant-1")); },
            (_, _) => { boundaryCalled = true; return Task.FromResult<string?>("Global"); },
            TimeSpan.FromSeconds(1));

        Assert.Equal(new DataBoundaryResolution("EU", DataBoundaryOutcome.Confirmed), resolution);
        Assert.False(tokenCalled);
        Assert.False(boundaryCalled);
    }

    [Fact]
    public async Task ResolveAsync_CachesBoundary_WhenTokenTenantMatches()
    {
        using var home = new TestHome();

        var resolution = await home.Resolver.ResolveAsync(
            "tenant-1",
            _ => Task.FromResult<string?>(JwtForTenant("tenant-1")),
            (_, _) => Task.FromResult<string?>("EU"),
            TimeSpan.FromSeconds(1));

        Assert.Equal(new DataBoundaryResolution("EU", DataBoundaryOutcome.Confirmed), resolution);
        Assert.Equal("EU", await home.Cache.GetAsync("tenant-1"));
    }

    [Fact]
    public async Task Cache_PersistsConfirmedBoundaryUnderTheSelectedHome()
    {
        using var home = new TestHome();

        await home.Cache.SetAsync("tenant-1", "eu");

        var cachePath = Path.Combine(home.Path, HomePaths.DataBoundaryCacheFileName);
        Assert.True(File.Exists(cachePath));
        var persisted = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(cachePath));
        Assert.NotNull(persisted);
        Assert.Equal("EU", persisted["tenant-1"]);
    }

    [Theory]
    [InlineData("EU", UploadRegion.EU)]
    [InlineData("Global", UploadRegion.ROW)]
    public async Task ResolveCachedUploadRegion_ReadsProfileAndCacheFromTheSameHome(
        string boundary, UploadRegion expected)
    {
        using var home = new TestHome();
        File.WriteAllText(Path.Combine(home.Path, HomePaths.ActiveProfileFileName), "selected");
        File.WriteAllText(Path.Combine(home.Path, HomePaths.ProfilesFileName),
            JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["selected"] = new { TenantId = "selected-tenant" },
            }));
        WriteCache(home.Path, new Dictionary<string, string> { ["selected-tenant"] = boundary });

        var (tenant, region) = await home.Resolver.ResolveCachedUploadRegionAsync();

        Assert.Equal("selected-tenant", tenant);
        Assert.Equal(expected, region);
    }

    [Fact]
    public async Task ResolveAsync_DoesNotCallBoundaryProvider_WhenTokenTenantDiffers()
    {
        using var home = new TestHome();
        var boundaryCalled = false;

        var resolution = await home.Resolver.ResolveAsync(
            "tenant-1",
            _ => Task.FromResult<string?>(JwtForTenant("tenant-2")),
            (_, _) => { boundaryCalled = true; return Task.FromResult<string?>("EU"); },
            TimeSpan.FromSeconds(1));

        Assert.Equal(new DataBoundaryResolution(null, DataBoundaryOutcome.AuthenticationUnavailable), resolution);
        Assert.False(boundaryCalled);
        Assert.Null(await home.Cache.GetAsync("tenant-1"));
    }

    [Fact]
    public async Task ResolveAndCacheAsync_PreservesBoundaryOnlyCompatibility()
    {
        using var home = new TestHome();
        await home.Cache.SetAsync("tenant-1", "EU");

        var boundary = await home.Resolver.ResolveAndCacheAsync(
            "tenant-1",
            _ => throw new InvalidOperationException("Cached boundaries must not request a token."),
            TimeSpan.FromSeconds(1));

        Assert.Equal("EU", boundary);
        Assert.Null(await home.Resolver.ResolveAndCacheAsync(
            "tenant-2", _ => Task.FromResult<string?>(null), TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void ResolveAriaExporter_DoesNotBorrowTheActiveProfileBoundary()
    {
        using var home = new TestHome();
        WriteCache(home.Path, new Dictionary<string, string> { ["tenant-1"] = "Global" });
        File.WriteAllText(Path.Combine(home.Path, HomePaths.ActiveProfileFileName), "test");
        File.WriteAllText(Path.Combine(home.Path, HomePaths.ProfilesFileName),
            JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["test"] = new { TenantId = "tenant-1" },
            }));

        Assert.Equal(WellKnownEndpoints.AriaEuEndpoint, TelemetryExporters.ResolveAriaExporter(UploadRegion.EU).Endpoint);
    }

    private static void WriteCache(string home, Dictionary<string, string> cache)
    {
        File.WriteAllText(
            Path.Combine(home, HomePaths.DataBoundaryCacheFileName),
            JsonSerializer.Serialize(cache));
    }

    private static string JwtForTenant(string tenantId)
    {
        var header = Base64Url("{}");
        var payload = Base64Url(JsonSerializer.Serialize(new { tid = tenantId }));
        return $"{header}.{payload}.signature";
    }

    private static string Base64Url(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed class TestHome : IDisposable
    {
        public TestHome()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "azclips-data-boundary-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            var persistence = new FilePersistenceStore(Path);
            Cache = new DataBoundaryCache(persistence);
            Resolver = new DataBoundaryResolver(persistence, new ProfileService(new NullLoggingService(), persistence));
        }

        public string Path { get; }
        public DataBoundaryCache Cache { get; }
        public DataBoundaryResolver Resolver { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}