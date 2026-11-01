// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using Core.Abstractions;

namespace Core.Telemetry.Tests;

[CollectionDefinition(nameof(DataBoundaryResolverTests), DisableParallelization = true)]
public sealed class DataBoundaryResolverTestsCollection { }

[Collection(nameof(DataBoundaryResolverTests))]
public class DataBoundaryResolverTests
{
    [Theory]
    [InlineData("EU", "EU")]
    [InlineData("Global", "ROW")]
    [InlineData("NotDefined", "ROW")]
    [InlineData("ROW", "ROW")]
    public async Task ResolveAndCacheAsync_ReturnsCachedBoundary_AfterAuthenticationWithoutCallingBoundaryProvider(string cached, string expected)
    {
        using var home = new TestHome();
        WriteCache(home.Path, new Dictionary<string, string> { ["tenant-1"] = cached });
        var tokenCalled = false;
        var boundaryCalled = false;

        var boundary = await DataBoundaryResolver.ResolveAndCacheAsync(
            "tenant-1",
            _ => { tokenCalled = true; return Task.FromResult<string?>(JwtForTenant("tenant-1")); },
            (_, _) => { boundaryCalled = true; return Task.FromResult<string?>("Global"); },
            TimeSpan.FromSeconds(1));

        Assert.Equal(expected, boundary);
        Assert.Equal(expected == "EU" ? WellKnownEndpoints.AriaEuEndpoint : WellKnownEndpoints.AriaRowEndpoint,
            TelemetryExporters.ResolveAriaExporter(boundary).Endpoint);
        Assert.True(tokenCalled);
        Assert.False(boundaryCalled);
    }

    [Fact]
    public void ResolveAriaExporter_UsesRow_WhenNoActiveProfile()
    {
        using var home = new TestHome();

        Assert.Equal(WellKnownEndpoints.AriaRowEndpoint, TelemetryExporters.ResolveAriaExporter().Endpoint);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveAndCacheAsync_UsesRowWithoutCallingProviders_WhenTenantIsMissing(string? tenantId)
    {
        using var home = new TestHome();
        var tokenCalled = false;
        var boundaryCalled = false;

        var boundary = await DataBoundaryResolver.ResolveAndCacheAsync(
            tenantId,
            _ => { tokenCalled = true; return Task.FromResult<string?>(JwtForTenant("tenant-1")); },
            (_, _) => { boundaryCalled = true; return Task.FromResult<string?>("EU"); },
            TimeSpan.FromSeconds(1));

        Assert.Equal("ROW", boundary);
        Assert.Equal(WellKnownEndpoints.AriaRowEndpoint, TelemetryExporters.ResolveAriaExporter(boundary).Endpoint);
        Assert.False(tokenCalled);
        Assert.False(boundaryCalled);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ResolveAndCacheAsync_UsesRowWithoutCachingFailure_WhenSilentAuthenticationFails(bool throws, bool cachedEu)
    {
        using var home = new TestHome();
        if (cachedEu)
            WriteCache(home.Path, new Dictionary<string, string> { ["tenant-1"] = "EU" });
        var boundaryCalled = false;

        var boundary = await DataBoundaryResolver.ResolveAndCacheAsync(
            "tenant-1",
            _ => throws ? throw new InvalidOperationException("Silent authentication failed.") : Task.FromResult<string?>(null),
            (_, _) => { boundaryCalled = true; return Task.FromResult<string?>("EU"); },
            TimeSpan.FromSeconds(1));

        Assert.Equal("ROW", boundary);
        Assert.Equal(WellKnownEndpoints.AriaRowEndpoint, TelemetryExporters.ResolveAriaExporter(boundary).Endpoint);
        Assert.False(boundaryCalled);
        Assert.Equal(cachedEu ? "EU" : null, DataBoundaryCache.Get("tenant-1"));
    }

    [Theory]
    [InlineData("EU", "EU")]
    [InlineData("eu", "EU")]
    [InlineData("Global", "ROW")]
    [InlineData("global", "ROW")]
    [InlineData("NotDefined", "ROW")]
    [InlineData("notdefined", "ROW")]
    public async Task ResolveAndCacheAsync_CachesBoundary_WhenTokenTenantMatches(string response, string expected)
    {
        using var home = new TestHome();

        var boundary = await DataBoundaryResolver.ResolveAndCacheAsync(
            "tenant-1",
            _ => Task.FromResult<string?>(JwtForTenant("tenant-1")),
            (_, _) => Task.FromResult<string?>(response),
            TimeSpan.FromSeconds(1));

        Assert.Equal(expected, boundary);
        Assert.Equal(expected, DataBoundaryCache.Get("tenant-1"));
        Assert.Equal(expected == "EU" ? WellKnownEndpoints.AriaEuEndpoint : WellKnownEndpoints.AriaRowEndpoint,
            TelemetryExporters.ResolveAriaExporter(boundary).Endpoint);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ResolveAndCacheAsync_UsesRowWithoutCallingBoundaryProvider_WhenTokenIsInvalid(bool cachedEu, bool malformed)
    {
        using var home = new TestHome();
        if (cachedEu)
            WriteCache(home.Path, new Dictionary<string, string> { ["tenant-1"] = "EU" });
        var boundaryCalled = false;

        var boundary = await DataBoundaryResolver.ResolveAndCacheAsync(
            "tenant-1",
            _ => Task.FromResult<string?>(malformed ? "invalid-token" : JwtForTenant("tenant-2")),
            (_, _) => { boundaryCalled = true; return Task.FromResult<string?>("EU"); },
            TimeSpan.FromSeconds(1));

        Assert.Equal("ROW", boundary);
        Assert.Equal(WellKnownEndpoints.AriaRowEndpoint, TelemetryExporters.ResolveAriaExporter(boundary).Endpoint);
        Assert.False(boundaryCalled);
        Assert.Equal(cachedEu ? "EU" : null, DataBoundaryCache.Get("tenant-1"));
    }

    [Fact]
    public async Task ResolveAndCacheAsync_UsesEuWithoutCachingFailure_WhenBoundaryProviderTimesOut()
    {
        using var home = new TestHome();

        var boundary = await DataBoundaryResolver.ResolveAndCacheAsync(
            "tenant-1",
            _ => Task.FromResult<string?>(JwtForTenant("tenant-1")),
            async (_, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                return "EU";
            },
            TimeSpan.FromMilliseconds(20));

        Assert.Equal("EU", boundary);
        Assert.Equal(WellKnownEndpoints.AriaEuEndpoint, TelemetryExporters.ResolveAriaExporter(boundary).Endpoint);
        Assert.Null(DataBoundaryCache.Get("tenant-1"));
    }

    [Fact]
    public async Task ResolveAndCacheAsync_UsesRow_WhenAuthenticationTimesOutDespiteCachedEu()
    {
        using var home = new TestHome();
        WriteCache(home.Path, new Dictionary<string, string> { ["tenant-1"] = "EU" });
        var boundaryCalled = false;

        var boundary = await DataBoundaryResolver.ResolveAndCacheAsync(
            "tenant-1",
            async cancellationToken =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return JwtForTenant("tenant-1");
            },
            (_, _) => { boundaryCalled = true; return Task.FromResult<string?>("EU"); },
            TimeSpan.FromMilliseconds(20));

        Assert.Equal("ROW", boundary);
        Assert.Equal(WellKnownEndpoints.AriaRowEndpoint, TelemetryExporters.ResolveAriaExporter(boundary).Endpoint);
        Assert.False(boundaryCalled);
        Assert.Equal("EU", DataBoundaryCache.Get("tenant-1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Unknown")]
    [InlineData("APAC")]
    [InlineData("ROW")]
    public async Task ResolveAndCacheAsync_UsesEuWithoutCachingFailure_WhenApiResponseIsUnusable(string? response)
    {
        using var home = new TestHome();

        var boundary = await DataBoundaryResolver.ResolveAndCacheAsync(
            "tenant-1",
            _ => Task.FromResult<string?>(JwtForTenant("tenant-1")),
            (_, _) => Task.FromResult(response),
            TimeSpan.FromSeconds(1));

        Assert.Equal("EU", boundary);
        Assert.Equal(WellKnownEndpoints.AriaEuEndpoint, TelemetryExporters.ResolveAriaExporter(boundary).Endpoint);
        Assert.Null(DataBoundaryCache.Get("tenant-1"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResolveAndCacheAsync_RetriesAfterApiFailure_AndCachesOnlyTheSuccessfulResponse(bool throws)
    {
        using var home = new TestHome();
        var boundaryCalls = 0;

        Task<string?> Lookup(string _, CancellationToken cancellationToken)
        {
            if (++boundaryCalls == 1)
            {
                if (throws)
                    throw new HttpRequestException("Boundary API failed.", null, System.Net.HttpStatusCode.ServiceUnavailable);
                return Task.FromResult<string?>(null);
            }
            return Task.FromResult<string?>("Global");
        }

        var first = await DataBoundaryResolver.ResolveAndCacheAsync(
            "tenant-1", _ => Task.FromResult<string?>(JwtForTenant("tenant-1")), Lookup, TimeSpan.FromSeconds(1));

        Assert.Equal("EU", first);
        Assert.Equal(WellKnownEndpoints.AriaEuEndpoint, TelemetryExporters.ResolveAriaExporter(first).Endpoint);
        Assert.Null(DataBoundaryCache.Get("tenant-1"));

        var second = await DataBoundaryResolver.ResolveAndCacheAsync(
            "tenant-1", _ => Task.FromResult<string?>(JwtForTenant("tenant-1")), Lookup, TimeSpan.FromSeconds(1));

        Assert.Equal("ROW", second);
        Assert.Equal(WellKnownEndpoints.AriaRowEndpoint, TelemetryExporters.ResolveAriaExporter(second).Endpoint);
        Assert.Equal("ROW", DataBoundaryCache.Get("tenant-1"));
        Assert.Equal(2, boundaryCalls);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("APAC")]
    public async Task ResolveAndCacheAsync_RefreshesUnusableCachedBoundary(string cached)
    {
        using var home = new TestHome();
        WriteCache(home.Path, new Dictionary<string, string> { ["tenant-1"] = cached });
        var boundaryCalled = false;

        var boundary = await DataBoundaryResolver.ResolveAndCacheAsync(
            "tenant-1",
            _ => Task.FromResult<string?>(JwtForTenant("tenant-1")),
            (_, _) => { boundaryCalled = true; return Task.FromResult<string?>("EU"); },
            TimeSpan.FromSeconds(1));

        Assert.True(boundaryCalled);
        Assert.Equal("EU", boundary);
        Assert.Equal("EU", DataBoundaryCache.Get("tenant-1"));
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
        private readonly string? _previousHome = Environment.GetEnvironmentVariable(HomePaths.HomeOverrideVariable);

        public TestHome()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "azclips-data-boundary-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            Environment.SetEnvironmentVariable(HomePaths.HomeOverrideVariable, Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(HomePaths.HomeOverrideVariable, _previousHome);
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
