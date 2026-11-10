// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azclips.Cli.Export;
using Core.Abstractions;
using Core.Cli.Help;

namespace Azclips.Help.Tool.Tests;

public sealed class HelpFileMapperTests
{
    [Theory]
    [InlineData("name", new string[] { }, "--name")]
    [InlineData("name", new[] { "n" }, "--name -n")]
    [InlineData("resource-group", new[] { "g" }, "--resource-group -g")]
    [InlineData("subscription-id", new[] { "s", "subscription" }, "--subscription-id -s -subscription")]
    [InlineData("tenant-id", new[] { "t", "tenant" }, "--tenant-id -t -tenant")]
    [InlineData("identity", new[] { "i" }, "--identity -i")]
    [InlineData("name", new[] { "-vm-name", "n" }, "--name --vm-name -n")]
    public void ParameterNamesPreserveAliases(string name, string[] aliases, string expected)
    {
        var manifest = Manifest();
        manifest.Commands[0].Parameters.Add(new HelpParameter(
            Name: name,
            PsName: "Parameter",
            Type: "string",
            Required: true,
            Description: "Parameter description",
            Aliases: aliases,
            AllowedValues: ["first", "second"],
            ParameterSetName: "Default",
            SupportsPipeline: false,
            SupportsWildcards: false,
            GroupName: "Parameter Arguments",
            ValueSources: "source"));

        var parameter = HelpFileMapper.Map(manifest).Last().Parameters!.First();

        Assert.Equal(expected, parameter.Name);
        Assert.True(parameter.Required);
        Assert.Equal("Parameter description", parameter.ShortSummary);
        Assert.Equal("first, second", parameter.Choices);
        Assert.Equal("Parameter Arguments", parameter.GroupName);
        Assert.Equal("source", parameter.ValueSources);
    }

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

    [Fact]
    public void SyntheticManifest_PreservesGroupsDescriptionsAndCliExamples()
    {
        var manifest = Manifest(HelpTag.GA);
        manifest.Commands[0] = manifest.Commands[0] with
        {
            Description = "A stable command description.",
            Examples = new HelpExampleSet(
                [
                    new HelpExample("Create an account", "az storage account create --name example"),
                    new HelpExample("Create and inspect", "az storage account create --name other\naz storage account show --name other")
                ],
                [new HelpExample("PowerShell only", "New-AzStorageAccount -Name example")])
        };

        var entries = HelpFileMapper.Map(manifest, subgroup => $"Summary for {subgroup}").ToArray();

        Assert.Equal(new[] { "storage", "storage account", "storage account create" }, entries.Select(entry => entry.Command));
        Assert.Equal(new[] { "group", "group", "command" }, entries.Select(entry => entry.Type));
        Assert.Equal("Storage", entries[0].ShortSummary);
        Assert.Equal("Summary for storage account", entries[1].ShortSummary);
        Assert.Null(entries[0].Parameters);
        Assert.Null(entries[1].Examples);
        var command = entries[2];
        Assert.Equal("Create", command.ShortSummary);
        Assert.Equal("A stable command description.", command.LongSummary);
        Assert.Null(command.CommandSource);
        Assert.NotNull(command.Examples);
        Assert.Collection(command.Examples,
            example => Assert.Equal(
                new HelpFileExample("Create an account", "az storage account create --name example"), example),
            example => Assert.Equal(
                new HelpFileExample("Create and inspect", "az storage account create --name other\naz storage account show --name other"), example));
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
