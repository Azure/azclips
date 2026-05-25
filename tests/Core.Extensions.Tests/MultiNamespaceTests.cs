// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Extensions;

namespace Core.Extensions.Tests
{
    /// <summary>
    /// Phase 9: a single registry entry may declare contributions to more than one top-level
    /// namespace. These tests prove the model holds end-to-end at the index / resolver layer
    /// using an in-memory Compute-shaped entry (<c>vm</c> + <c>disk</c>).
    /// </summary>
    public class MultiNamespaceTests
    {
        private static readonly IReadOnlyDictionary<string, BuiltInExtension> EmptyBuiltIns =
            new Dictionary<string, BuiltInExtension>(StringComparer.OrdinalIgnoreCase);

        private sealed class InMemoryStore(ExtensionRegistry registry) : IExtensionRegistryStore
        {
            public ExtensionRegistry Saved { get; private set; } = registry;
            public ExtensionRegistry Load() => Saved;
            public void Save(ExtensionRegistry registry) => Saved = registry;
        }

        private static ExtensionRegistryEntry MultiNamespaceEntry() => new()
        {
            Verb = "vm",
            Version = "1.0.0",
            InstalledAt = DateTimeOffset.UtcNow,
            Contributions =
            {
                new Contribution { Verb = "vm",   Path = "create" },
                new Contribution { Verb = "vm",   Path = "list" },
                new Contribution { Verb = "vm",   Path = "show" },
                new Contribution { Verb = "vm",   Path = "delete" },
                new Contribution { Verb = "disk", Path = "list" },
            },
            Namespaces = { "vm", "disk" },
        };

        [Fact]
        public void LeafIndex_ShouldRouteBothNamespaces_FromSingleEntry()
        {
            var registry = new ExtensionRegistry();
            registry.Extensions["Microsoft.Azure.Cirrus.Extensions.Compute"] = MultiNamespaceEntry();

            var index = LeafIndex.Build(EmptyBuiltIns, registry);

            Assert.True(index.TryGet("vm", "create", out var vmCreate));
            Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Compute", vmCreate!.PackageId);

            Assert.True(index.TryGet("disk", "list", out var diskList));
            Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Compute", diskList!.PackageId);
        }

        [Fact]
        public void ExtensionResolver_ShouldResolveBothVerbs_ToSamePackage()
        {
            var registry = new ExtensionRegistry();
            registry.Extensions["Microsoft.Azure.Cirrus.Extensions.Compute"] = MultiNamespaceEntry();
            var resolver = new ExtensionResolver(new InMemoryStore(registry), EmptyBuiltIns);

            Assert.True(resolver.TryResolve("vm", out var vm));
            Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Compute", vm!.PackageId);

            Assert.True(resolver.TryResolve("disk", out var disk));
            Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Compute", disk!.PackageId);
        }

        [Fact]
        public void ExtensionResolver_ShouldFallBackToLegacyVerb_WhenNamespacesEmpty()
        {
            // v1 entries (no Namespaces, no Contributions) still resolve via the legacy Verb field.
            var registry = new ExtensionRegistry();
            registry.Extensions["Legacy.Package"] = new ExtensionRegistryEntry
            {
                Verb = "legacy",
                Version = "1.0.0",
                InstalledAt = DateTimeOffset.UtcNow,
            };
            var resolver = new ExtensionResolver(new InMemoryStore(registry), EmptyBuiltIns);

            Assert.True(resolver.TryResolve("legacy", out var legacy));
            Assert.Equal("Legacy.Package", legacy!.PackageId);
        }

        [Fact]
        public void Save_Roundtrip_ShouldPreserveBothNamespaces()
        {
            // Persisting and reloading a multi-namespace entry keeps every contribution.
            var tmp = Path.Combine(Path.GetTempPath(), "cirrus-multi-ns-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tmp);
                var path = Path.Combine(tmp, "registry.json");

                var store1 = new FileExtensionRegistryStore(path);
                var registry = store1.Load();
                registry.Extensions["Microsoft.Azure.Cirrus.Extensions.Compute"] = MultiNamespaceEntry();
                store1.Save(registry);

                var store2 = new FileExtensionRegistryStore(path);
                var reloaded = store2.Load();
                var entry = reloaded.Extensions["Microsoft.Azure.Cirrus.Extensions.Compute"];

                Assert.Equal(2, entry.Namespaces.Count);
                Assert.Contains("vm", entry.Namespaces, StringComparer.OrdinalIgnoreCase);
                Assert.Contains("disk", entry.Namespaces, StringComparer.OrdinalIgnoreCase);
                Assert.Equal(5, entry.Contributions.Count);
                Assert.Contains(entry.Contributions, c => c.Verb == "disk" && c.Path == "list");
            }
            finally
            {
                if (Directory.Exists(tmp)) Directory.Delete(tmp, recursive: true);
            }
        }

        [Fact]
        public void RemoveEntry_ShouldPurgeBothNamespaces_InSingleOperation()
        {
            // `extension remove` removes the package key from registry.Extensions; both
            // namespaces stop resolving in the same step.
            var registry = new ExtensionRegistry();
            registry.Extensions["Microsoft.Azure.Cirrus.Extensions.Compute"] = MultiNamespaceEntry();
            var store = new InMemoryStore(registry);
            var resolver = new ExtensionResolver(store, EmptyBuiltIns);

            Assert.True(resolver.TryResolve("vm", out _));
            Assert.True(resolver.TryResolve("disk", out _));

            registry.Extensions.Remove("Microsoft.Azure.Cirrus.Extensions.Compute");
            store.Save(registry);

            Assert.False(resolver.TryResolve("vm", out _));
            Assert.False(resolver.TryResolve("disk", out _));
        }
    }
}
