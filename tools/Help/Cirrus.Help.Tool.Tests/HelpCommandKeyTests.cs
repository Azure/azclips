// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Cirrus.Help.Tool.Validation;

namespace Cirrus.Help.Tool.Tests
{
    public class HelpCommandKeyTests
    {
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("   ", "")]
        [InlineData("login", "login")]
        [InlineData("Login", "login")]
        [InlineData("  Config Get  ", "config get")]
        public void Normalize_ShouldTrimAndLowercase_WhenGivenAnyInput(string? input, string expected)
        {
            Assert.Equal(expected, HelpCommandKey.Normalize(input));
        }

        [Theory]
        [InlineData("", "index.md")]
        [InlineData("login", "login.md")]
        [InlineData("config get", "config-get.md")]
        [InlineData("Group Role Assignment Create", "group-role-assignment-create.md")]
        public void ToHelpFileName_ShouldMapKeyToFilename_WhenGivenCommandKey(string key, string expected)
        {
            Assert.Equal(expected, HelpCommandKey.ToHelpFileName(key));
        }

        [Theory]
        [InlineData("index.md", "")]
        [InlineData("login.md", "login")]
        [InlineData("config-get.md", "config get")]
        [InlineData("INDEX.MD", "")]
        public void FromHelpFileName_ShouldInvertToHelpFileName_WhenGivenFilename(string fileName, string expected)
        {
            Assert.Equal(expected, HelpCommandKey.FromHelpFileName(fileName));
        }

        [Theory]
        [InlineData("login", "Login")]
        [InlineData("config", "Config")]
        [InlineData("a", "A")]
        [InlineData("", "")]
        public void ToExtensionFolder_ShouldPascalCase_WhenGivenExtensionName(string input, string expected)
        {
            Assert.Equal(expected, HelpCommandKey.ToExtensionFolder(input));
        }
    }
}
