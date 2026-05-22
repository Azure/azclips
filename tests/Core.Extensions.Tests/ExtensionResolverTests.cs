// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Extensions;

namespace Core.Extensions.Tests
{
    public class ExtensionResolverTests
    {
        private static readonly IReadOnlyDictionary<string, BuiltInExtension> TestBuiltIns =
            new Dictionary<string, BuiltInExtension>(StringComparer.OrdinalIgnoreCase)
            {
                ["login"] = new("login", "Microsoft.Azure.Cirrus.Extensions.Login"),
                ["config"] = new("config", "Microsoft.Azure.Cirrus.Extensions.Config"),
            };

        private sealed class InMemoryStore(ExtensionRegistry registry) : IExtensionRegistryStore
        {
            public ExtensionRegistry Saved { get; private set; } = registry;
            public ExtensionRegistry Load() => Saved;
            public void Save(ExtensionRegistry registry) => Saved = registry;
        }

        private static InMemoryStore EmptyStore() => new(new ExtensionRegistry());

        private static InMemoryStore StoreWith(string packageId, string verb, string version = "1.0.0")
        {
            var registry = new ExtensionRegistry();
            registry.Extensions[packageId] = new ExtensionRegistryEntry
            {
                Verb = verb,
                Version = version,
                InstalledAt = DateTimeOffset.UtcNow,
            };
            return new InMemoryStore(registry);
        }

        [Fact]
        public void TryResolve_ShouldReturnBuiltIn_WhenVerbIsInSeed()
        {
            var resolver = new ExtensionResolver(EmptyStore(), TestBuiltIns);

            var found = resolver.TryResolve("login", out var resolved);

            Assert.True(found);
            Assert.NotNull(resolved);
            Assert.True(resolved!.IsBuiltIn);
            Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Login", resolved.PackageId);
            Assert.Null(resolved.Version);
        }

        [Fact]
        public void TryResolve_ShouldMatchVerb_CaseInsensitively()
        {
            var resolver = new ExtensionResolver(EmptyStore(), TestBuiltIns);

            Assert.True(resolver.TryResolve("LOGIN", out _));
            Assert.True(resolver.TryResolve("Login", out _));
        }

        [Fact]
        public void TryResolve_ShouldReturnRegistryEntry_WhenVerbIsInstalled()
        {
            var store = StoreWith("Microsoft.Azure.Cirrus.Extensions.Compute", "vm", "1.1.0");
            var resolver = new ExtensionResolver(store, TestBuiltIns);

            var found = resolver.TryResolve("vm", out var resolved);

            Assert.True(found);
            Assert.NotNull(resolved);
            Assert.False(resolved!.IsBuiltIn);
            Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Compute", resolved.PackageId);
            Assert.Equal("1.1.0", resolved.Version);
        }

        [Fact]
        public void TryResolve_ShouldPreferBuiltIn_OnConflict()
        {
            // A bogus installed extension claiming "login" must lose to the built-in.
            var store = StoreWith("Bogus.Login", "login");
            var resolver = new ExtensionResolver(store, TestBuiltIns);

            var found = resolver.TryResolve("login", out var resolved);

            Assert.True(found);
            Assert.True(resolved!.IsBuiltIn);
            Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Login", resolved.PackageId);
        }

        [Fact]
        public void TryResolve_ShouldReturnFalse_WhenVerbUnknown()
        {
            var resolver = new ExtensionResolver(EmptyStore(), TestBuiltIns);

            var found = resolver.TryResolve("foo", out var resolved);

            Assert.False(found);
            Assert.Null(resolved);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void TryResolve_ShouldReturnFalse_ForBlankOrNullVerb(string? verb)
        {
            var resolver = new ExtensionResolver(EmptyStore(), TestBuiltIns);

            var found = resolver.TryResolve(verb!, out var resolved);

            Assert.False(found);
            Assert.Null(resolved);
        }

        [Fact]
        public void DefaultBuiltIns_ShouldContainExactlyCoreThree()
        {
            // Phase 4 #15: the seed is exactly extension/login/config.
            // group (Resources) and vm (Compute) are installable extensions, not built-ins.
            Assert.Contains("extension", BuiltInExtensions.Map.Keys);
            Assert.Contains("login", BuiltInExtensions.Map.Keys);
            Assert.Contains("config", BuiltInExtensions.Map.Keys);
            Assert.DoesNotContain("group", BuiltInExtensions.Map.Keys);
            Assert.DoesNotContain("vm", BuiltInExtensions.Map.Keys);
            Assert.Equal(3, BuiltInExtensions.Map.Count);
        }

        [Fact]
        public void IsBuiltInVerb_ShouldBeCaseInsensitive()
        {
            Assert.True(BuiltInExtensions.IsBuiltInVerb("LOGIN"));
            Assert.True(BuiltInExtensions.IsBuiltInVerb("Config"));
            Assert.False(BuiltInExtensions.IsBuiltInVerb("vm"));
            Assert.False(BuiltInExtensions.IsBuiltInVerb("nonexistent"));
        }
    }
}
