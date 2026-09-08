// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Extensions;
using Tests.Common.Utilities;

namespace Core.Extensions.Tests
{
    public class ExtensionResolverTests
    {
        private static InMemoryRegistryStore EmptyStore() => new(new ExtensionRegistry());

        private static InMemoryRegistryStore StoreWith(string packageId, string verb, string version = "1.0.0")
        {
            var registry = new ExtensionRegistry();
            registry.Extensions[packageId] = new ExtensionRegistryEntry
            {
                Verb = verb,
                Version = version,
                InstalledAt = DateTimeOffset.UtcNow,
            };
            return new InMemoryRegistryStore(registry);
        }

        [Fact]
        public void TryResolve_ShouldReturnRegistryEntry_WhenVerbIsInstalled()
        {
            var store = StoreWith("Microsoft.Azure.Azclips.Extensions.Compute", "vm", "1.1.0");
            var resolver = new ExtensionResolver(store);

            var found = resolver.TryResolve("vm", out var resolved);

            Assert.True(found);
            Assert.NotNull(resolved);
            Assert.Equal("Microsoft.Azure.Azclips.Extensions.Compute", resolved!.PackageId);
            Assert.Equal("1.1.0", resolved.Version);
        }

        [Fact]
        public void TryResolve_ShouldReturnRegistryEntry_ForFormerCoreVerb()
        {
            // Post-CLIPS-622: Login is a normal extension — same code path as vm/group/etc.
            // The rule the test enshrines is "no Core exception in resolution": every verb
            // resolves through the same registry lookup.
            var store = StoreWith("Microsoft.Azure.Azclips.Extensions.Accounts", "login");
            var resolver = new ExtensionResolver(store);

            var found = resolver.TryResolve("login", out var resolved);

            Assert.True(found);
            Assert.Equal("Microsoft.Azure.Azclips.Extensions.Accounts", resolved!.PackageId);
        }

        [Fact]
        public void TryResolve_ShouldReturnFalse_ForFormerCoreVerb_WhenRegistryEmpty()
        {
            // Guard against reintroducing a hidden built-in seed. An empty registry means truly
            // nothing resolves — including former-Core verbs. If this fails, someone likely put
            // back a static fallback map in ExtensionResolver.
            var resolver = new ExtensionResolver(EmptyStore());

            Assert.False(resolver.TryResolve("login", out _));
            Assert.False(resolver.TryResolve("config", out _));
            Assert.False(resolver.TryResolve("extension", out _));
        }

        [Fact]
        public void TryResolve_ShouldMatchVerb_CaseInsensitively()
        {
            var store = StoreWith("Microsoft.Azure.Azclips.Extensions.Accounts", "login");
            var resolver = new ExtensionResolver(store);

            Assert.True(resolver.TryResolve("LOGIN", out _));
            Assert.True(resolver.TryResolve("Login", out _));
            Assert.True(resolver.TryResolve("login", out _));
        }

        [Fact]
        public void TryResolve_ShouldReturnFalse_WhenVerbUnknown()
        {
            var resolver = new ExtensionResolver(EmptyStore());

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
            var resolver = new ExtensionResolver(EmptyStore());

            var found = resolver.TryResolve(verb!, out var resolved);

            Assert.False(found);
            Assert.Null(resolved);
        }
    }
}
