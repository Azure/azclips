// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Extensions;

namespace Core.Extensions.Tests
{
    public class LeafReaderTests
    {
        [Theory]
        [InlineData("vm create", "vm", "create")]
        [InlineData("login", "login", "")]
        [InlineData("extension remove", "extension", "remove")]
        [InlineData("  config get  ", "config", "get")]
        public void SplitLeafKey_ShouldExtractVerbAndPath(string input, string expectedVerb, string expectedPath)
        {
            var (verb, path) = LeafReader.SplitLeafKey(input);
            Assert.Equal(expectedVerb, verb);
            Assert.Equal(expectedPath, path);
        }

        [Fact]
        public void SplitLeafKey_ShouldThrow_OnEmptyInput()
        {
            Assert.Throws<ArgumentException>(() => LeafReader.SplitLeafKey(""));
            Assert.Throws<ArgumentException>(() => LeafReader.SplitLeafKey("   "));
        }

        [Fact]
        public void ReadCliLeafKeys_ShouldThrow_WhenDirectoryMissing()
        {
            var missing = Path.Combine(Path.GetTempPath(), "leafreader-nope-" + Guid.NewGuid().ToString("N"));
            Assert.Throws<DirectoryNotFoundException>(() => LeafReader.ReadCliLeafKeys(missing));
        }

        [Fact]
        public void ReadCliLeafKeys_ShouldReturnEmpty_ForEmptyDirectory()
        {
            var dir = Directory.CreateTempSubdirectory("leafreader-empty-").FullName;
            try
            {
                var keys = LeafReader.ReadCliLeafKeys(dir);
                Assert.Empty(keys);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
