// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Runtime;
using Core.Runtime.Persistence;
using Tests.Common.Stubs;
using Tests.Common.Utilities;
using Xunit;

namespace Cli.Tests;

public sealed class BenchmarkProfileStoreTests : IDisposable
{
    private readonly string _dir;

    public BenchmarkProfileStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "azclips-benchmark-store-tests", Guid.NewGuid().ToString("N"));
    }

    private static ProfileService NewService(string dir) => new(new NullLoggingService(), new FilePersistenceStore(dir));

    [Fact]
    public void GetRoot_ShouldPointAtBenchmarkHome_WhenGivenRepoRoot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "fake-repo");

        var root = BenchmarkProfileStore.GetRoot(repoRoot);

        Assert.Equal(
            Path.Combine(repoRoot, "artifacts", "Benchmark", "home", ".azclips"),
            root);
    }

    [Fact]
    public async Task SeedAsync_ShouldWriteBothStoreFiles_WhenDirectoryDoesNotExist()
    {
        await BenchmarkProfileStore.SeedAsync(_dir);

        Assert.True(File.Exists(Path.Combine(_dir, "profiles.json")));
        Assert.True(File.Exists(Path.Combine(_dir, "active-profile")));
    }

    [Fact]
    public async Task SeedAsync_ShouldProduceResolvableActiveProfile_WhenReadByFreshService()
    {
        await BenchmarkProfileStore.SeedAsync(_dir);

        var active = await NewService(_dir).GetActiveProfileAsync();

        Assert.NotNull(active);
        Assert.Equal(BenchmarkProfileStore.ProfileName, active!.Name);
        Assert.Equal(BenchmarkProfileStore.BenchmarkSubscriptionId, active.SubscriptionId);
        Assert.Equal(BenchmarkProfileStore.BenchmarkTenantId, active.TenantId);
        Assert.Equal("AzureCloud", active.CloudName);
        Assert.Equal(AuthMethod.Browser, active.AuthMethod);
    }

    [Fact]
    public async Task SeedAsync_ShouldRemoveProfilesWrittenByAPreviousIteration()
    {
        await NewService(_dir).SaveProfileAsync(new Profile
        {
            Name = "leftover",
            TenantId = "t",
            SubscriptionId = Guid.NewGuid().ToString(),
            CloudName = "AzureCloud",
            AuthMethod = AuthMethod.Browser,
        });

        await BenchmarkProfileStore.SeedAsync(_dir);

        var all = await NewService(_dir).GetAllProfilesAsync();
        Assert.Equal(BenchmarkProfileStore.ProfileName, Assert.Single(all).Name);
    }

    [Fact]
    public async Task SeedAsync_ShouldRestoreActivePointer_WhenAPreviousIterationClearedIt()
    {
        await BenchmarkProfileStore.SeedAsync(_dir);
        await NewService(_dir).SetActiveProfileNameAsync(null);

        await BenchmarkProfileStore.SeedAsync(_dir);

        Assert.Equal(
            BenchmarkProfileStore.ProfileName,
            await NewService(_dir).GetActiveProfileNameAsync());
    }

    [Fact]
    public async Task SeedAsync_ShouldBeIdempotent_WhenCalledRepeatedly()
    {
        await BenchmarkProfileStore.SeedAsync(_dir);
        await BenchmarkProfileStore.SeedAsync(_dir);
        await BenchmarkProfileStore.SeedAsync(_dir);

        Assert.Single(await NewService(_dir).GetAllProfilesAsync());
    }

    [Fact]
    public async Task VerifySeededAsync_ShouldSucceed_WhenStoreIsSeeded()
    {
        await BenchmarkProfileStore.SeedAsync(_dir);

        var ex = await Record.ExceptionAsync(() => BenchmarkProfileStore.VerifySeededAsync(_dir));

        Assert.Null(ex);
    }

    [Fact]
    public async Task VerifySeededAsync_ShouldThrow_WhenStoreIsEmpty()
    {
        Directory.CreateDirectory(_dir);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => BenchmarkProfileStore.VerifySeededAsync(_dir));

        Assert.Contains(_dir, ex.Message);
    }

    [Fact]
    public async Task VerifySeededAsync_ShouldThrow_WhenActivePointerNamesAMissingProfile()
    {
        await BenchmarkProfileStore.SeedAsync(_dir);
        await NewService(_dir).RemoveProfileAsync(BenchmarkProfileStore.ProfileName);
        await NewService(_dir).SetActiveProfileNameAsync(BenchmarkProfileStore.ProfileName);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => BenchmarkProfileStore.VerifySeededAsync(_dir));
    }

    [Fact]
    public async Task Cleanup_ShouldRemoveTheStoreDirectory()
    {
        await BenchmarkProfileStore.SeedAsync(_dir);

        BenchmarkProfileStore.Cleanup(_dir);

        Assert.False(Directory.Exists(_dir));
    }

    [Fact]
    public void Cleanup_ShouldNotThrow_WhenDirectoryIsAbsent()
    {
        var ex = Record.Exception(() => BenchmarkProfileStore.Cleanup(Path.Combine(_dir, "never-created")));

        Assert.Null(ex);
    }

    [Fact]
    public async Task SeedAsync_ShouldStampUserPrincipalName_WhenOneIsSupplied()
    {
        await BenchmarkProfileStore.SeedAsync(_dir, "benchmark@contoso.com");

        var active = await NewService(_dir).GetActiveProfileAsync();

        Assert.NotNull(active);
        Assert.Equal("benchmark@contoso.com", active!.UserPrincipalName);
    }

    [Fact]
    public async Task SeedAsync_ShouldLeaveUserPrincipalNameNull_WhenNoneIsSupplied()
    {
        await BenchmarkProfileStore.SeedAsync(_dir);

        var active = await NewService(_dir).GetActiveProfileAsync();

        Assert.NotNull(active);
        Assert.Null(active!.UserPrincipalName);
    }

    [Fact]
    public async Task VerifySeededAsync_ShouldThrow_WhenSeededUserPrincipalNameDoesNotMatch()
    {
        await BenchmarkProfileStore.SeedAsync(_dir, "someone-else@contoso.com");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => BenchmarkProfileStore.VerifySeededAsync(_dir, "benchmark@contoso.com"));

        Assert.Contains("benchmark@contoso.com", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifySeededAsync_ShouldSucceed_WhenSeededUserPrincipalNameMatches()
    {
        await BenchmarkProfileStore.SeedAsync(_dir, "benchmark@contoso.com");

        var ex = await Record.ExceptionAsync(
            () => BenchmarkProfileStore.VerifySeededAsync(_dir, "benchmark@contoso.com"));

        Assert.Null(ex);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }
        catch (IOException) { /* best-effort */ }
        catch (UnauthorizedAccessException) { /* best-effort */ }
    }
}
