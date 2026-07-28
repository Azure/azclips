// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
                ["login"] = new("login", "Microsoft.Azure.Azclips.Extensions.Login"),
                ["config"] = new("config", "Microsoft.Azure.Azclips.Extensions.Config"),
            };

            var index = LeafIndex.Build(builtIns, new ExtensionRegistry());

            Assert.True(index.TryGet("login", "", out var login));
            Assert.NotNull(login);
            Assert.Equal("Microsoft.Azure.Azclips.Extensions.Login", login!.PackageId);

            Assert.True(index.TryGet("config", "", out _));
            Assert.False(index.TryGet("vm", "", out _));
        }

        [Fact]
        public void Build_ShouldMergeRegistryContributions_WithBuiltIns()
        {
            var builtIns = new Dictionary<string, BuiltInExtension>(StringComparer.OrdinalIgnoreCase)
            {
                ["login"] = new("login", "Microsoft.Azure.Azclips.Extensions.Login"),
            };

            var registry = new ExtensionRegistry
            {
                Extensions =
                {
                    ["Microsoft.Azure.Azclips.Extensions.Compute"] = new ExtensionRegistryEntry
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
            Assert.Equal("Microsoft.Azure.Azclips.Extensions.Compute", vmCreate!.PackageId);
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
                ["login"] = new("login", "Microsoft.Azure.Azclips.Extensions.Login"),
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

        [Fact]
        public void HasVerb_ShouldReturnTrue_WhenAnyLeafClaimsTheVerb()
        {
            var registry = new ExtensionRegistry
            {
                Extensions =
                {
                    ["Microsoft.Azure.Azclips.Extensions.Compute"] = new ExtensionRegistryEntry
                    {
                        Version = "1.0.0",
                        InstalledAt = DateTimeOffset.UtcNow,
                        Contributions =
                        {
                            new Contribution { Verb = "vm",   Path = "create" },
                            new Contribution { Verb = "vm",   Path = "list" },
                            new Contribution { Verb = "disk", Path = "attach" },
                        },
                    },
                },
            };

            var index = LeafIndex.Build(new Dictionary<string, BuiltInExtension>(), registry);

            Assert.True(index.HasVerb("vm"));
            Assert.True(index.HasVerb("disk"));
            Assert.False(index.HasVerb("network"));
        }

        [Fact]
        public void HasVerb_ShouldMatch_CaseInsensitively()
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

            Assert.True(index.HasVerb("vm"));
            Assert.True(index.HasVerb("VM"));
            Assert.True(index.HasVerb("Vm"));
        }

        [Fact]
        public void HasVerb_ShouldReturnFalse_ForNullOrEmpty()
        {
            var index = LeafIndex.Build(new Dictionary<string, BuiltInExtension>(), new ExtensionRegistry());

            Assert.False(index.HasVerb(string.Empty));
            Assert.False(index.HasVerb(null!));
        }

        [Fact]
        public void HasVerb_ShouldReturnTrue_ForBuiltInVerbs()
        {
            var builtIns = new Dictionary<string, BuiltInExtension>(StringComparer.OrdinalIgnoreCase)
            {
                ["login"] = new("login", "Microsoft.Azure.Azclips.Extensions.Login"),
            };

            var index = LeafIndex.Build(builtIns, new ExtensionRegistry());

            Assert.True(index.HasVerb("login"));
            Assert.False(index.HasVerb("vm"));
        }

        // ── IsCommandGroup ──
        // A built-in whose only leaf is its root (login) is runnable; one that owns longer leaves
        // (agent) is a group at the root and at every intermediate path.

        private static LeafIndex BuildAgentAndLoginIndex()
        {
            var builtIns = new Dictionary<string, BuiltInExtension>(StringComparer.OrdinalIgnoreCase)
            {
                ["login"] = new("login", "Microsoft.Azure.Azclips.Extensions.Login"),
                ["agent"] = new("agent", "Microsoft.Azure.Azclips.Extensions.Agent")
                {
                    Leaves = new[] { "tool env", "tool resource find", "tool resource show" },
                },
            };

            return LeafIndex.Build(builtIns, new ExtensionRegistry());
        }

        [Fact]
        public void IsCommandGroup_ShouldReturnTrue_ForGroupRootAndIntermediatePaths()
        {
            var index = BuildAgentAndLoginIndex();

            Assert.True(index.IsCommandGroup("agent", ""));                 // az agent
            Assert.True(index.IsCommandGroup("agent", "tool"));             // az agent tool
            Assert.True(index.IsCommandGroup("agent", "tool resource"));    // az agent tool resource
        }

        [Fact]
        public void IsCommandGroup_ShouldReturnFalse_ForRunnableLeaves()
        {
            var index = BuildAgentAndLoginIndex();

            Assert.False(index.IsCommandGroup("agent", "tool env"));            // runnable leaf
            Assert.False(index.IsCommandGroup("agent", "tool resource find"));  // runnable leaf
        }

        [Fact]
        public void IsCommandGroup_ShouldReturnFalse_ForRunnableRootVerb()
        {
            var index = BuildAgentAndLoginIndex();

            // login's only leaf is its root, so `az login` is a runnable command, not a group.
            Assert.False(index.IsCommandGroup("login", ""));
        }

        [Fact]
        public void IsCommandGroup_ShouldReturnFalse_ForUnknownLeafUnderGroup()
        {
            var index = BuildAgentAndLoginIndex();

            // `tool resource findxxx` is neither a leaf nor a group prefix — a typo, not a group.
            Assert.False(index.IsCommandGroup("agent", "tool resource findxxx"));
        }

        [Fact]
        public void IsCommandGroup_ShouldMatch_CaseInsensitively()
        {
            var index = BuildAgentAndLoginIndex();

            Assert.True(index.IsCommandGroup("AGENT", "Tool Resource"));
        }

        [Fact]
        public void IsCommandGroup_ShouldReturnFalse_ForNullOrEmptyVerb()
        {
            var index = BuildAgentAndLoginIndex();

            Assert.False(index.IsCommandGroup(string.Empty, ""));
            Assert.False(index.IsCommandGroup(null!, ""));
        }
    }
}

