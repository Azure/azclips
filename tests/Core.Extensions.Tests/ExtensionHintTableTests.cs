// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Extensions;

namespace Core.Extensions.Tests
{
    public class ExtensionHintTableTests
    {
        [Fact]
        public void TryGetPackageId_Verb_ShouldReturnTrue_ForExistingVerb()
        {
            var table = new ExtensionHintTable
            {
                Leaves = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["vm"] = "Microsoft.Azure.Cirrus.Extensions.Compute",
                },
            };

            Assert.True(table.TryGetPackageId("vm", out var pkg));
            Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Compute", pkg);

            Assert.True(table.TryGetPackageId("VM", out _)); // case-insensitive
        }

        [Fact]
        public void TryGetPackageId_VerbAndPath_ShouldReturnTrue_ForLeafKey()
        {
            var table = new ExtensionHintTable
            {
                Leaves = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["vm create"] = "Microsoft.Azure.Cirrus.Extensions.Compute",
                    ["vm scale"]  = "Microsoft.Azure.Cirrus.Extensions.ComputeFuture",
                },
            };

            Assert.True(table.TryGetPackageId("vm", "create", out var pkg));
            Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Compute", pkg);

            Assert.True(table.TryGetPackageId("VM", "SCALE", out var pkg2));
            Assert.Equal("Microsoft.Azure.Cirrus.Extensions.ComputeFuture", pkg2);
        }

        [Fact]
        public void TryGetPackageId_VerbAndPath_ShouldFallBackToBareVerb_WhenPathEmpty()
        {
            var table = new ExtensionHintTable
            {
                Leaves = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["login"] = "Microsoft.Azure.Cirrus.Extensions.Login",
                },
            };

            Assert.True(table.TryGetPackageId("login", "", out var pkg));
            Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Login", pkg);
        }

        [Fact]
        public void TryGetPackageId_ShouldReturnFalse_WhenMissing()
        {
            var table = new ExtensionHintTable();
            Assert.False(table.TryGetPackageId("nope", out var pkg1));
            Assert.Equal(string.Empty, pkg1);
            Assert.False(table.TryGetPackageId("nope", "either", out var pkg2));
            Assert.Equal(string.Empty, pkg2);
        }

        [Fact]
        public void Default_ShouldBeNonNull_WithLeavesInitialized()
        {
            var table = ExtensionHintTable.Default;
            Assert.NotNull(table);
            Assert.NotNull(table.Leaves);
        }
    }
}
