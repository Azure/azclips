// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Extensions;
using Microsoft.Azure.Cirrus.Extensions.Extension.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Regression: <c>extension update</c> must persist the registry before purging old payload
/// directories. Pre-fix order was <c>PurgeSiblingVersions(...)</c> then <c>store.Save(registry)</c>;
/// if Save threw (concurrency conflict) the user lost the old payload AND had no recorded new
/// install.
/// </summary>
public class UpdatePersistAndPurgeTests
{
    private const string PackageId = "Microsoft.Azure.Cirrus.Extensions.Resources";

    private sealed class ThrowingStore : IExtensionRegistryStore
    {
        public ExtensionRegistry Load() => new();
        public void Save(ExtensionRegistry registry)
            => throw new InvalidOperationException("simulated concurrency conflict");
    }

    private sealed class CountingStore : IExtensionRegistryStore
    {
        public int SaveCount { get; private set; }
        public ExtensionRegistry? LastSaved { get; private set; }
        public ExtensionRegistry Load() => new();
        public void Save(ExtensionRegistry registry)
        {
            SaveCount++;
            LastSaved = registry;
        }
    }

    private static ExtensionRegistryEntry NewEntry(string version) => new()
    {
        Verb = "deployment",
        Version = version,
        InstalledAt = DateTimeOffset.UtcNow,
        Contributions = { new Contribution { Verb = "deployment", Path = "group create" } },
    };

    [Fact]
    public void PersistAndPurge_ShouldNotInvokePurge_WhenSaveThrows()
    {
        var registry = new ExtensionRegistry();
        var store = new ThrowingStore();
        var purgeRan = false;

        Assert.Throws<InvalidOperationException>(() =>
            UpdateExtensionProcessor.PersistAndPurge(
                store,
                registry,
                PackageId,
                NewEntry("1.1.0"),
                () => purgeRan = true));

        Assert.False(purgeRan);
    }

    [Fact]
    public void PersistAndPurge_ShouldInvokePurgeExactlyOnce_AfterSuccessfulSave()
    {
        var registry = new ExtensionRegistry();
        var store = new CountingStore();
        var purgeCount = 0;

        UpdateExtensionProcessor.PersistAndPurge(
            store,
            registry,
            PackageId,
            NewEntry("1.1.0"),
            () => purgeCount++);

        Assert.Equal(1, store.SaveCount);
        Assert.Equal(1, purgeCount);
    }

    [Fact]
    public void PersistAndPurge_ShouldUpdateRegistryEntry_BeforeSave()
    {
        var registry = new ExtensionRegistry();
        var store = new CountingStore();

        UpdateExtensionProcessor.PersistAndPurge(
            store,
            registry,
            PackageId,
            NewEntry("1.1.0"),
            purgeAction: () => { });

        Assert.NotNull(store.LastSaved);
        Assert.True(store.LastSaved!.Extensions.ContainsKey(PackageId));
        Assert.Equal("1.1.0", store.LastSaved.Extensions[PackageId].Version);
    }

    [Fact]
    public void PersistAndPurge_ShouldThrowArgumentNullException_OnNullArgs()
    {
        var registry = new ExtensionRegistry();
        var store = new CountingStore();
        var entry = NewEntry("1.0.0");
        Action purge = () => { };

        Assert.Throws<ArgumentNullException>(
            () => UpdateExtensionProcessor.PersistAndPurge(null!, registry, PackageId, entry, purge));
        Assert.Throws<ArgumentNullException>(
            () => UpdateExtensionProcessor.PersistAndPurge(store, null!, PackageId, entry, purge));
        Assert.Throws<ArgumentNullException>(
            () => UpdateExtensionProcessor.PersistAndPurge(store, registry, PackageId, null!, purge));
        Assert.Throws<ArgumentNullException>(
            () => UpdateExtensionProcessor.PersistAndPurge(store, registry, PackageId, entry, null!));
    }
}
