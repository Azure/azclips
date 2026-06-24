// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Extensions;

namespace Core.Extensions.Tests
{
    public class RegistryReconcilerTests
    {
        [Fact]
        public void Reconcile_ShouldMarkEntryStale_WhenInstallDirectoryMissing()
        {
            var installRoot = Directory.CreateTempSubdirectory("reconcile-").FullName;
            try
            {
                // pkg-A exists on disk; pkg-B does not.
                Directory.CreateDirectory(Path.Combine(installRoot, "PkgA", "1.0.0"));

                var registry = new ExtensionRegistry
                {
                    Extensions = new Dictionary<string, ExtensionRegistryEntry>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["PkgA"] = new() { Verb = "a", Version = "1.0.0" },
                        ["PkgB"] = new() { Verb = "b", Version = "2.0.0" },
                    },
                };

                var result = RegistryReconciler.Reconcile(registry, installRoot);

                Assert.Equal(2, result.Live.Extensions.Count);
                Assert.True(result.Live.Extensions.ContainsKey("PkgA"));
                Assert.True(result.Live.Extensions.ContainsKey("PkgB"));

                Assert.Empty(result.Stale);
            }
            finally
            {
                Directory.Delete(installRoot, recursive: true);
            }
        }

        [Fact]
        public void Reconcile_ShouldReturnAllLive_WhenEveryPayloadPresent()
        {
            var installRoot = Directory.CreateTempSubdirectory("reconcile-allgood-").FullName;
            try
            {
                Directory.CreateDirectory(Path.Combine(installRoot, "PkgA", "1.0.0"));
                Directory.CreateDirectory(Path.Combine(installRoot, "PkgB", "2.0.0"));

                var registry = new ExtensionRegistry
                {
                    Extensions = new Dictionary<string, ExtensionRegistryEntry>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["PkgA"] = new() { Verb = "a", Version = "1.0.0" },
                        ["PkgB"] = new() { Verb = "b", Version = "2.0.0" },
                    },
                };

                var result = RegistryReconciler.Reconcile(registry, installRoot);

                Assert.Equal(2, result.Live.Extensions.Count);
                Assert.Empty(result.Stale);
            }
            finally
            {
                Directory.Delete(installRoot, recursive: true);
            }
        }

        [Fact]
        public void Reconcile_ShouldReturnEmptyLive_WhenRegistryEmpty()
        {
            var installRoot = Directory.CreateTempSubdirectory("reconcile-empty-").FullName;
            try
            {
                var result = RegistryReconciler.Reconcile(new ExtensionRegistry(), installRoot);
                Assert.Empty(result.Live.Extensions);
                Assert.Empty(result.Stale);
            }
            finally
            {
                Directory.Delete(installRoot, recursive: true);
            }
        }

        [Fact]
        public void Reconcile_ShouldNotMutate_InputRegistry()
        {
            var installRoot = Directory.CreateTempSubdirectory("reconcile-immutable-").FullName;
            try
            {
                var registry = new ExtensionRegistry
                {
                    Extensions = new Dictionary<string, ExtensionRegistryEntry>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["PkgA"] = new() { Verb = "a", Version = "1.0.0" },
                    },
                };
                var originalCount = registry.Extensions.Count;

                _ = RegistryReconciler.Reconcile(registry, installRoot);

                Assert.Equal(originalCount, registry.Extensions.Count);
                Assert.True(registry.Extensions.ContainsKey("PkgA"));
            }
            finally
            {
                Directory.Delete(installRoot, recursive: true);
            }
        }
    }
}
