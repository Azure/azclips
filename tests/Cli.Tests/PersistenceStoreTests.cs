// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Authentication;
using Core.Runtime.Persistence;
using Core.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cli.Tests;

[Collection("Azclips Service Provider Tests")]
public sealed class PersistenceStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "azclips-persistence-tests", Guid.NewGuid().ToString("N"));
    private FilePersistenceStore CreateStore() => new(_root);

    [Fact]
    public void AuthenticationDoesNotReferenceRuntimeAssembly()
    {
        Assert.DoesNotContain(typeof(MsalClientOptions).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name == "Core.Runtime");
    }

    [Fact]
    public async Task TokenCacheFactoryUsesInjectedPersistenceInterface()
    {
        var persistence = new Mock<IPersistenceStore>(MockBehavior.Strict);
        persistence.Setup(store => store.GetFilePath("msal.cache.user.json"))
            .Returns(Path.Combine(_root, "msal.cache.user.json"));
        persistence.Setup(store => store.EnsureDirectoryAsync("", It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Yield();
                Directory.CreateDirectory(_root);
                return _root;
            });
        var previous = MsalTokenCacheFactory.ForcePlaintextForTests;
        try
        {
            MsalTokenCacheFactory.ForcePlaintextForTests = true;
            var resolution = await MsalTokenCacheFactory.CreateAsync(persistence.Object);
            Assert.NotNull(resolution.Helper);
            persistence.Verify(store => store.EnsureDirectoryAsync("", It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            MsalTokenCacheFactory.ForcePlaintextForTests = previous;
        }
    }

    [Fact]
    public async Task EnsureDirectoryAsyncCreatesAndReturnsDirectoryIdempotently()
    {
        IPersistenceStore store = CreateStore();
        var expected = Path.Combine(_root, "component", "cache");
        Assert.Equal(expected, await store.EnsureDirectoryAsync(Path.Combine("component", "cache")));
        Assert.True(Directory.Exists(expected));
        Assert.Equal(expected, await store.EnsureDirectoryAsync(Path.Combine("component", "cache")));
        Assert.Equal(_root, await store.EnsureDirectoryAsync());
    }

    [Fact]
    public async Task CancelledDirectoryCreationDoesNotCreateRoot()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateStore().EnsureDirectoryAsync("component", cancellation.Token));
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task CancelledDeletePreservesExistingFile()
    {
        var store = CreateStore();
        await store.WriteAsync("state", new byte[] { 1 });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.DeleteAsync("state", cancellation.Token));
        Assert.Equal(new byte[] { 1 }, await store.ReadAsync("state"));
    }

    [Fact]
    public void PersistenceImplementationLivesInRuntimeAssembly()
    {
        Assert.Same(typeof(ProfileService).Assembly, typeof(FilePersistenceStore).Assembly);
    }

    [Fact]
    public void DefaultRootUsesHome()
    {
        Assert.Equal(Path.GetFullPath(HomePaths.GetDefaultHomeRoot()), new FilePersistenceStore().RootDirectory);
    }

    [Fact]
    public void HomeOverrideIsHonored()
    {
        var previous = Environment.GetEnvironmentVariable(HomePaths.HomeOverrideVariable);
        try
        {
            Environment.SetEnvironmentVariable(HomePaths.HomeOverrideVariable, _root);
            Assert.Equal(_root, new FilePersistenceStore().RootDirectory);
            Assert.Equal(_root, new MsalClientOptions(new FilePersistenceStore()).Persistence.RootDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable(HomePaths.HomeOverrideVariable, previous);
        }
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("nested/../../outside")]
    [InlineData("")]
    [InlineData(".")]
    public void InvalidFilePathsAreRejected(string path)
    {
        Assert.Throws<ArgumentException>(() => CreateStore().GetFilePath(path));
    }

    [Fact]
    public void AbsolutePathsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => CreateStore().GetFilePath(Path.GetFullPath("outside")));
    }

    [Fact]
    public async Task MissingReadAndDeleteAreHarmless()
    {
        var store = CreateStore();
        Assert.Null(await store.ReadAsync("missing"));
        await store.DeleteAsync("missing");
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task WritesCreateParentsAndReplaceCompleteContents()
    {
        var store = CreateStore();
        await store.WriteAsync("component/state", new byte[] { 1, 2, 3 });
        await store.WriteAsync("component/state", new byte[] { 4 });
        Assert.Equal(new byte[] { 4 }, await store.ReadAsync("component/state"));
        Assert.Single(Directory.GetFiles(Path.Combine(_root, "component")));
        await store.DeleteAsync("component/state");
        Assert.Null(await store.ReadAsync("component/state"));
    }

    [Fact]
    public async Task ConcurrentAccessNeverReadsPartialWrites()
    {
        var store = CreateStore();
        var first = Enumerable.Repeat((byte)1, 128 * 1024).ToArray();
        var second = Enumerable.Repeat((byte)2, first.Length).ToArray();
        await store.WriteAsync("state", first);
        var writer = Task.Run(async () =>
        {
            for (var i = 0; i < 32; i++)
            {
                await store.WriteAsync("state", i % 2 == 0 ? second : first);
            }
        });
        for (var i = 0; i < 64; i++)
        {
            var content = await store.ReadAsync("state");
            Assert.NotNull(content);
            Assert.Equal(first.Length, content.Length);
            Assert.True(content.SequenceEqual(first) || content.SequenceEqual(second));
        }
        await writer;
    }

    [Fact]
    public async Task UnixFilesArePrivateAndSymbolicLinksAreRejected()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        var store = CreateStore();
        await store.WriteAsync("state", new byte[] { 1 });
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(store.GetFilePath("state")));
        Directory.CreateSymbolicLink(Path.Combine(_root, "link"), Path.GetTempPath());
        Assert.Throws<ArgumentException>(() => store.GetFilePath("link/outside"));
        File.CreateSymbolicLink(Path.Combine(_root, "file-link"), store.GetFilePath("state"));
        await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteAsync("file-link"));
    }

    [Fact]
    public async Task CancelledWritePreservesExistingFile()
    {
        var store = CreateStore();
        await store.WriteAsync("state", new byte[] { 1 });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.WriteAsync("state", new byte[] { 2 }, cancellation.Token));
        Assert.Equal(new byte[] { 1 }, await store.ReadAsync("state"));
        Assert.Single(Directory.GetFiles(_root));
    }

    [Fact]
    public async Task FailedReplacementSurfacesErrorAndCleansTemporaryFile()
    {
        var store = CreateStore();
        await store.EnsureDirectoryAsync("state");
        await Assert.ThrowsAnyAsync<IOException>(() => store.WriteAsync("state", new byte[] { 1 }));
        Assert.Empty(Directory.GetFiles(_root));
    }

    [Fact]
    public async Task ProfilesUseInjectedStore()
    {
        var store = CreateStore();
        var profileService = new ProfileService(new ConsoleLoggingService(), store);
        await profileService.SaveAndActivateProfileAsync(new Profile { Name = "test" });
        Assert.NotNull(await store.ReadAsync(HomePaths.ProfilesFileName));
        Assert.Equal("test", await store.ReadTextAsync(HomePaths.ActiveProfileFileName));
        Assert.Equal("test", (await profileService.GetActiveProfileAsync())?.Name);
    }

    [Fact]
    public async Task ProfilesStillReadBomEncodedState()
    {
        var store = CreateStore();
        await store.EnsureDirectoryAsync();
        await File.WriteAllTextAsync(store.GetFilePath(HomePaths.ProfilesFileName),
            """{"test":{"Name":"test"}}""", System.Text.Encoding.Unicode);
        var profileService = new ProfileService(new ConsoleLoggingService(), store);
        Assert.Equal("test", (await profileService.GetProfileAsync("test"))?.Name);
    }

    [Fact]
    public void RuntimeAndAuthenticationShareOverriddenStore()
    {
        var services = new ServiceCollection();
        var store = CreateStore();
        services.RegisterRuntimeServices();
        services.RegisterAuthenticationServices();
        services.AddSingleton<IPersistenceStore>(store);
        using var provider = services.BuildServiceProvider();
        Assert.Same(store, provider.GetRequiredService<IPersistenceStore>());
        Assert.Same(store, provider.GetRequiredService<MsalClientOptions>().Persistence);
        Assert.IsType<ProfileService>(provider.GetRequiredService<IProfileService>());
    }

    [Theory]
    [InlineData("msal.cache.user")]
    [InlineData("msal.cache.app")]
    [InlineData("service.principal.entries")]
    public void CachePartitionsDefaultToHome(string prefix)
    {
        Assert.Equal(
            Path.Combine(HomePaths.GetDefaultHomeRoot(), prefix + ".json"),
            MsalTokenCacheFactory.GetCacheFilePath(true, new FilePersistenceStore(), prefix));
    }

    [Theory]
    [InlineData("msal.cache.user")]
    [InlineData("msal.cache.app")]
    [InlineData("service.principal.entries")]
    public async Task CachePartitionsPersistInNewRoot(string prefix)
    {
        var previous = Environment.GetEnvironmentVariable(HomePaths.HomeOverrideVariable);
        var previousPlaintext = MsalTokenCacheFactory.ForcePlaintextForTests;
        try
        {
            Environment.SetEnvironmentVariable(HomePaths.HomeOverrideVariable, _root);
            MsalTokenCacheFactory.ForcePlaintextForTests = true;
            var resolution = await MsalTokenCacheFactory.CreateAsync(new FilePersistenceStore(), prefix);
            Assert.NotNull(resolution.Helper);
            resolution.Helper.SaveUnencryptedTokenCache("{}"u8.ToArray());
            Assert.NotNull(await CreateStore().ReadAsync(prefix + ".json"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(HomePaths.HomeOverrideVariable, previous);
            MsalTokenCacheFactory.ForcePlaintextForTests = previousPlaintext;
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
