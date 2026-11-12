// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Help;

namespace Cli.Tests;

public class ExtensionIndexHelpAdapterTests
{
    public static IEnumerable<object[]> CommandCases()
    {
        (Type ProcessorType, HelpTag[] Tags)[] processors =
        [
            (typeof(PreviewProcessor), [HelpTag.Preview]),
            (typeof(ExperimentalProcessor), [HelpTag.Experimental]),
            (typeof(DeprecatedProcessor), [HelpTag.Deprecated]),
            (typeof(GaProcessor), [HelpTag.GA]),
            (typeof(MultipleTagsProcessor), [HelpTag.Deprecated, HelpTag.Preview]),
            (typeof(UntaggedProcessor), []),
            (typeof(NoAttributeProcessor), []),
        ];

        foreach (var (processorType, tags) in processors)
        {
            foreach (var commandKey in new[] { "lifecycle-test", "lifecycle-test create", "lifecycle-test subgroup create" })
            {
                yield return [processorType, tags, commandKey];
            }
        }
    }

    [Theory]
    [MemberData(nameof(CommandCases))]
    public void GetCommandHelp_ShouldPreserveProcessorTags(Type processorType, HelpTag[] expectedTags, string commandKey)
    {
        var adapter = CreateAdapter(processorType, commandKey);

        var command = adapter.GetCommandHelp(commandKey.Split(' '));

        Assert.NotNull(command);
        Assert.Equal(expectedTags, command.Tags);
    }

    [Theory]
    [MemberData(nameof(CommandCases))]
    public void GetGroupHelp_ShouldPreserveAndRenderProcessorTags(Type processorType, HelpTag[] expectedTags, string commandKey)
    {
        var adapter = CreateAdapter(processorType, commandKey);
        var commandPath = commandKey.Split(' ');
        var groupPath = commandPath.Length == 1 ? commandPath : commandPath[..^1];

        var group = adapter.GetGroupHelp(groupPath);

        Assert.NotNull(group);
        var summary = Assert.Single(group.Commands);
        Assert.Equal(commandPath[^1], summary.Name);
        Assert.Equal(expectedTags, summary.Tags);
        Assert.Equal(commandPath.Length == 1 ? expectedTags : [], adapter.GetRootGroup().RootCommandTags);

        if (groupPath.Length == 1)
        {
            Assert.Equal(expectedTags, Assert.Single(adapter.GetRootGroup().Commands).Tags);
        }

        var renderer = new RuntimeHelpRenderer();
        var tagSuffix = expectedTags.Length == 0 ? string.Empty : $" [{string.Join(",", expectedTags)}]";
        var expectedSummary = processorType == typeof(NoAttributeProcessor) ? "Test" : "Test command.";
        Assert.Equal(
            ["# COMMANDS", $"{summary.Name}{tagSuffix}:", expectedSummary],
            renderer.RenderCommandsSection(group.Commands, terminalWidth: null));
        Assert.Contains(
            $"{summary.Name}{tagSuffix}:",
            renderer.RenderGroup(group, terminalWidth: null));
    }

    [Fact]
    public void RenderCommand_ShouldNotChangeStandaloneFormatting_WhenProcessorHasTags()
    {
        var adapter = CreateAdapter(typeof(MultipleTagsProcessor), "lifecycle-test create");
        var command = adapter.GetCommandHelp(["lifecycle-test", "create"]);
        Assert.NotNull(command);
        var renderer = new RuntimeHelpRenderer();

        Assert.Equal(
            renderer.RenderCommand(command with { Tags = [] }, terminalWidth: null),
            renderer.RenderCommand(command, terminalWidth: null));
    }

    private static ExtensionIndexHelpAdapter CreateAdapter(Type processorType, string commandKey)
        => new(
            new Dictionary<string, CommandMetadata>
            {
                [commandKey] = new(
                    Name: "TestCommand",
                    InvocationName: commandKey,
                    Aliases: [],
                    FullName: processorType.FullName ?? processorType.Name,
                    AssemblyName: string.Empty,
                    ProcessorType: processorType,
                    Parameters: new Dictionary<string, ParameterMetadata>(),
                    ParameterAliases: new Dictionary<string, string>()),
            },
            "lifecycle-test");

    [ProcessorInfo(Summary = "Test command.", Tags = [HelpTag.Preview])]
    private sealed class PreviewProcessor { }

    [ProcessorInfo(Summary = "Test command.", Tags = [HelpTag.Experimental])]
    private sealed class ExperimentalProcessor { }

    [ProcessorInfo(Summary = "Test command.", Tags = [HelpTag.Deprecated])]
    private sealed class DeprecatedProcessor { }

    [ProcessorInfo(Summary = "Test command.", Tags = [HelpTag.GA])]
    private sealed class GaProcessor { }

    [ProcessorInfo(Summary = "Test command.", Tags = [HelpTag.Deprecated, HelpTag.Preview])]
    private sealed class MultipleTagsProcessor { }

    [ProcessorInfo(Summary = "Test command.")]
    private sealed class UntaggedProcessor { }

    private sealed class NoAttributeProcessor { }
}
