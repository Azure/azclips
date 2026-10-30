// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azclips.Cli.Export;
using Core.Abstractions;
using Core.Cli.Help;

namespace Azclips.Help.Tool.Tests;

public sealed class HelpFileMapperTests
{
    [Theory]
    [InlineData(HelpTag.Preview, "", null)]
    [InlineData(HelpTag.GA, null, null)]
    [InlineData(HelpTag.Experimental, null, "")]
    public void UntaggedCommandsAndGroupsInheritProductMaturity(
        HelpTag productMaturity, string? preview, string? experimental)
    {
        var entries = HelpFileMapper.Map(Manifest(), productMaturity: productMaturity)
            .Append(HelpFileMapper.CreateRootGroup(productMaturity));

        Assert.All(entries, entry =>
        {
            Assert.Equal(preview, entry.PreviewInfo);
            Assert.Equal(experimental, entry.ExperimentalInfo);
        });
    }

    [Theory]
    [InlineData(HelpTag.GA, HelpTag.Preview, null, null)]
    [InlineData(HelpTag.Preview, HelpTag.GA, "preview message", null)]
    [InlineData(HelpTag.Experimental, HelpTag.Preview, null, "experimental message")]
    public void ExplicitMaturityOverridesProduct(
        HelpTag tag, HelpTag productMaturity, string? preview, string? experimental)
    {
        var command = HelpFileMapper.Map(Manifest(tag), productMaturity: productMaturity).Last();

        Assert.Equal(preview, command.PreviewInfo);
        Assert.Equal(experimental, command.ExperimentalInfo);
    }

    [Fact]
    public void DeprecatedOnlyCommandInheritsPreviewAndPreservesBreakingChange()
    {
        var command = HelpFileMapper.Map(Manifest(HelpTag.Deprecated)).Last();

        Assert.Equal("", command.PreviewInfo);
        Assert.Equal("[Breaking Change] retired", command.DeprecateInfo);
        Assert.Equal(command.DeprecateInfo, command.BreakingChangeInfo);
    }

    [Theory]
    [InlineData(HelpTag.Preview, HelpTag.GA)]
    [InlineData(HelpTag.Preview, HelpTag.Experimental)]
    [InlineData(HelpTag.GA, HelpTag.Experimental)]
    public void ConflictingMaturityIsRejected(HelpTag first, HelpTag second)
    {
        Assert.Throws<ArgumentException>(() => HelpFileMapper.Map(Manifest(first, second)).ToArray());
    }

    [Fact]
    public void DeprecatedIsNotAProductMaturity()
    {
        Assert.Throws<ArgumentException>(() => HelpFileMapper.CreateRootGroup(HelpTag.Deprecated));
    }

    private static HelpManifest Manifest(params HelpTag[] tags) => new(
        Verb: "storage",
        Version: "1.0",
        RootSummary: "Storage",
        Commands:
        [
            new HelpCommand(
                CliName: "storage account create",
                PsName: "New-AzStorageAccount",
                Summary: "Create",
                Description: "",
                Parameters: [],
                Examples: new HelpExampleSet([], []),
                Tags: tags,
                PreviewMessage: tags.Contains(HelpTag.Preview) ? "preview message" : null,
                ExperimentalMessage: tags.Contains(HelpTag.Experimental) ? "experimental message" : null,
                DeprecatedMessage: "[Breaking Change] retired")
        ]);
}
