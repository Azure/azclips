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
    [Fact]
    public async Task ResolveAndCacheAsync_ReturnsCachedBoundary_WithoutCallingProviders()
    {
        using var home = new TestHome();
        WriteCache(home.Path, new Dictionary<string, string> { ["tenant-1"] = "EU" });
        var tokenCalled = false;
        var boundaryCalled = false;

        var boundary = await DataBoundaryResolver.ResolveAndCacheAsync(
            "tenant-1",
            _ => { tokenCalled = true; return Task.FromResult<string?>(JwtForTenant("tenant-1")); },
            (_, _) => { boundaryCalled = true; return Task.FromResult<string?>("Global"); },
            TimeSpan.FromSeconds(1));

        Assert.Equal("EU", boundary);
        Assert.False(tokenCalled);
        Assert.False(boundaryCalled);
    }

    [Fact]
    public async Task ResolveAndCacheAsync_CachesBoundary_WhenTokenTenantMatches()
    {
        using var home = new TestHome();

        var boundary = await DataBoundaryResolver.ResolveAndCacheAsync(
            "tenant-1",
            _ => Task.FromResult<string?>(JwtForTenant("tenant-1")),
            (_, _) => Task.FromResult<string?>("EU"),
            TimeSpan.FromSeconds(1));

        Assert.Equal("EU", boundary);
        Assert.Equal("EU", DataBoundaryCache.Get("tenant-1"));
    }

    [Fact]
    public async Task ResolveAndCacheAsync_DoesNotCallBoundaryProvider_WhenTokenTenantDiffers()
    {
        using var home = new TestHome();
        var boundaryCalled = false;

        var boundary = await DataBoundaryResolver.ResolveAndCacheAsync(
            "tenant-1",
            _ => Task.FromResult<string?>(JwtForTenant("tenant-2")),
            (_, _) => { boundaryCalled = true; return Task.FromResult<string?>("EU"); },
            TimeSpan.FromSeconds(1));

        Assert.Null(boundary);
        Assert.False(boundaryCalled);
        Assert.Null(DataBoundaryCache.Get("tenant-1"));
    }

    [Fact]
    public async Task ResolveAndCacheAsync_ReturnsNull_WhenBoundaryProviderTimesOut()
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

        Assert.Null(boundary);
        Assert.Null(DataBoundaryCache.Get("tenant-1"));
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