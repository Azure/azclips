// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Parser;
using Xunit;

namespace Cli.Tests.Parser;

/// <summary>
/// Drift-detection contract between <see cref="KnownGlobalFlags"/> (the docs-facing descriptor
/// list consumed by the runtime <c>--help</c> renderer and the docs exporter) and
/// <see cref="GlobalArgParser"/> (the actual parse behaviour). Adding a global flag to the
/// parser without listing it here, or vice versa, causes visible drift between docs and runtime
/// — these tests catch that.
/// </summary>
public class KnownGlobalFlagsTests
{
    [Fact]
    public void EveryKnownGlobalFlag_IsRecognisedByParser()
    {
        foreach (var flag in KnownGlobalFlags.All)
        {
            // --output requires a value; help flags are classified into Help but stay in Positional.
            var input = flag.Name == "--output"
                ? new[] { flag.Name, "json" }
                : new[] { flag.Name };

            var result = GlobalArgParser.Parse(input);

            var leaked = result.Positional.Contains(flag.Name);
            var classifiedAsHelp = result.Help is not null;

            Assert.True(
                !leaked || classifiedAsHelp,
                $"Global flag '{flag.Name}' leaked into positional and was not classified as help. " +
                "KnownGlobalFlags.All appears out of sync with GlobalArgParser.");
        }
    }

    [Theory]
    [InlineData("--debug")]
    [InlineData("--help")]
    [InlineData("--no-cache")]
    [InlineData("--output")]
    public void KnownGlobalFlags_ContainsExpectedFlagName(string expectedName)
    {
        Assert.Contains(KnownGlobalFlags.All, flag => flag.Name == expectedName);
    }
}
