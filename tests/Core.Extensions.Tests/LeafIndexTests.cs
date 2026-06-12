// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Extensions;

namespace Core.Extensions.Tests
{
    public class LeafIndexTests
    {
        [Fact]
        public void Build_ShouldEmitBuiltInsAsRootLeaves()
        {
            var builtIns = new Dictionary<string, BuiltInExtension>(StringComparer.OrdinalIgnoreCase)
            {
                ["login"] = new("login", "Microsoft.Azure.Cirrus.Extensions.Login"),
                ["config"] = new("config", "Microsoft.Azure.Cirrus.Extensions.Config"),
            };

            var index = LeafIndex.Build(builtIns, new ExtensionRegistry());

            Assert.True(index.TryGet("login", "", out var login));
            Assert.NotNull(login);
            Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Login", login!.PackageId);

            Assert.True(index.TryGet("config", "", out _));
            Assert.False(index.TryGet("vm", "", out _));
        }

        [Fact]
        public void Build_ShouldMergeRegistryContributions_WithBuiltIns()
        {
            var builtIns = new Dictionary<string, BuiltInExtension>(StringComparer.OrdinalIgnoreCase)
            {
                ["login"] = new("login", "Microsoft.Azure.Cirrus.Extensions.Login"),
            };

            var registry = new ExtensionRegistry
            {
                Extensions =
                {
                    ["Microsoft.Azure.Cirrus.Extensions.Compute"] = new ExtensionRegistryEntry
                    {
                        Version = "1.0.0",
                        InstalledAt = DateTimeOffset.UtcNow,
                        Contributions =
                        {
                            new Contribution { Verb = "vm",   Path = "create" },
                            new Contribution { Verb = "disk", Path = "attach" },
                        },
                    },
                },
            };

            var index = LeafIndex.Build(builtIns, registry);

            Assert.Equal(3, index.Entries.Count);
            Assert.True(index.TryGet("vm", "create", out var vmCreate));
            Assert.NotNull(vmCreate);
            Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Compute", vmCreate!.PackageId);
            Assert.True(index.TryGet("disk", "attach", out _));
        }

        [Fact]
        public void Build_ShouldLookup_CaseInsensitively()
        {
            var registry = new ExtensionRegistry
            {
                Extensions =
                {
                    ["Pkg"] = new ExtensionRegistryEntry
                    {
                        Version = "1.0.0",
                        InstalledAt = DateTimeOffset.UtcNow,
                        Contributions = { new Contribution { Verb = "VM", Path = "Create" } },
                    },
                },
            };

            var index = LeafIndex.Build(new Dictionary<string, BuiltInExtension>(), registry);

            Assert.True(index.TryGet("vm", "create", out _));
            Assert.True(index.TryGet("VM", "CREATE", out _));
        }

        [Fact]
        public void Build_ShouldThrow_OnDuplicateLeafBetweenExtensions()
        {
            var registry = new ExtensionRegistry
            {
                Extensions =
                {
                    ["Pkg.A"] = new ExtensionRegistryEntry
                    {
                        Version = "1.0.0",
                        InstalledAt = DateTimeOffset.UtcNow,
                        Contributions = { new Contribution { Verb = "vm", Path = "create" } },
                    },
                    ["Pkg.B"] = new ExtensionRegistryEntry
                    {
                        Version = "1.0.0",
                        InstalledAt = DateTimeOffset.UtcNow,
                        Contributions = { new Contribution { Verb = "vm", Path = "create" } },
                    },
                },
            };

            var ex = Assert.Throws<InvalidOperationException>(
                () => LeafIndex.Build(new Dictionary<string, BuiltInExtension>(), registry));
            Assert.Contains("vm", ex.Message, StringComparison.Ordinal);
            Assert.Contains("create", ex.Message, StringComparison.Ordinal);
            Assert.Contains("Pkg.A", ex.Message, StringComparison.Ordinal);
            Assert.Contains("Pkg.B", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Build_ShouldThrow_WhenRegistryClashesWithBuiltIn()
        {
            var builtIns = new Dictionary<string, BuiltInExtension>(StringComparer.OrdinalIgnoreCase)
            {
                ["login"] = new("login", "Microsoft.Azure.Cirrus.Extensions.Login"),
            };

            var registry = new ExtensionRegistry
            {
                Extensions =
                {
                    ["Rogue.Login"] = new ExtensionRegistryEntry
                    {
                        Version = "1.0.0",
                        InstalledAt = DateTimeOffset.UtcNow,
                        Contributions = { new Contribution { Verb = "login", Path = "" } },
                    },
                },
            };

            Assert.Throws<InvalidOperationException>(() => LeafIndex.Build(builtIns, registry));
        }

        [Fact]
        public void Build_ShouldAcceptV1Entry_AfterRegistryNormalization()
        {
            // Simulate what FileExtensionRegistryStore.Load() does to v1 entries: synthesize a
            // single (verb, "") contribution. LeafIndex.Build itself does not normalize.
            var entry = new ExtensionRegistryEntry
            {
                Verb = "vm",
                Version = "1.0.0",
                InstalledAt = DateTimeOffset.UtcNow,
                Contributions = { new Contribution { Verb = "vm", Path = string.Empty } },
                Namespaces = { "vm" },
            };
            var registry = new ExtensionRegistry { Extensions = { ["Pkg.Vm"] = entry } };

            var index = LeafIndex.Build(new Dictionary<string, BuiltInExtension>(), registry);

            Assert.True(index.TryGet("vm", "", out var vm));
            Assert.Equal("Pkg.Vm", vm!.PackageId);
        }
    }
}
