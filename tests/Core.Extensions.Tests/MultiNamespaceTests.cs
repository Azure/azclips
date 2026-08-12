// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Extensions;
using Tests.Common.Utilities;

namespace Core.Extensions.Tests
{
    /// <summary>
    /// A single registry entry may declare contributions to more than one top-level namespace.
    /// These tests exercise the model end-to-end at the index / resolver layer using an
    /// in-memory Compute-shaped entry (<c>vm</c> + <c>disk</c>).
    /// </summary>
    public class MultiNamespaceTests
    {
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
            registry.Extensions["Microsoft.Azure.Azclips.Extensions.Compute"] = MultiNamespaceEntry();

            var index = LeafIndex.Build(registry);

            Assert.True(index.TryGet("vm", "create", out var vmCreate));
            Assert.Equal("Microsoft.Azure.Azclips.Extensions.Compute", vmCreate!.PackageId);

            Assert.True(index.TryGet("disk", "list", out var diskList));
            Assert.Equal("Microsoft.Azure.Azclips.Extensions.Compute", diskList!.PackageId);
        }

        [Fact]
        public void ExtensionResolver_ShouldResolveBothVerbs_ToSamePackage()
        {
            var registry = new ExtensionRegistry();
            registry.Extensions["Microsoft.Azure.Azclips.Extensions.Compute"] = MultiNamespaceEntry();
            var resolver = new ExtensionResolver(new InMemoryRegistryStore(registry));

            Assert.True(resolver.TryResolve("vm", out var vm));
            Assert.Equal("Microsoft.Azure.Azclips.Extensions.Compute", vm!.PackageId);

            Assert.True(resolver.TryResolve("disk", out var disk));
            Assert.Equal("Microsoft.Azure.Azclips.Extensions.Compute", disk!.PackageId);
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
            var resolver = new ExtensionResolver(new InMemoryRegistryStore(registry));

            Assert.True(resolver.TryResolve("legacy", out var legacy));
            Assert.Equal("Legacy.Package", legacy!.PackageId);
        }

        [Fact]
        public void Save_Roundtrip_ShouldPreserveBothNamespaces()
        {
            // §5.2: Save strips installed-state fields; only preferences survive round-trip.
            var tmp = Path.Combine(Path.GetTempPath(), "azclips-multi-ns-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tmp);
                var path = Path.Combine(tmp, "registry.json");

                var store1 = new FileExtensionRegistryStore(path);
                var registry = store1.Load();
                registry.Extensions["Microsoft.Azure.Azclips.Extensions.Compute"] = MultiNamespaceEntry();
                store1.Save(registry);

                var store2 = new FileExtensionRegistryStore(path);
                var reloaded = store2.Load();
                var entry = reloaded.Extensions["Microsoft.Azure.Azclips.Extensions.Compute"];

                Assert.Empty(entry.Namespaces);
                Assert.Empty(entry.Contributions);
                Assert.Null(entry.Verb);
                Assert.Equal("1.0.0", entry.Version);
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
            registry.Extensions["Microsoft.Azure.Azclips.Extensions.Compute"] = MultiNamespaceEntry();
            var store = new InMemoryRegistryStore(registry);
            var resolver = new ExtensionResolver(store);

            Assert.True(resolver.TryResolve("vm", out _));
            Assert.True(resolver.TryResolve("disk", out _));

            registry.Extensions.Remove("Microsoft.Azure.Azclips.Extensions.Compute");
            store.Save(registry);

            Assert.False(resolver.TryResolve("vm", out _));
            Assert.False(resolver.TryResolve("disk", out _));
        }
    }
}

