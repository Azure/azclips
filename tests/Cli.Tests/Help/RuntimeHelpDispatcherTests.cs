// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Help;
using Core.Extensions;
using Core.Runtime;
using Moq;

namespace Cli.Tests;

[Collection("Azclips Service Provider Tests")]
public class RuntimeHelpDispatcherTests
{
    [Theory]
    [InlineData("")]
    [InlineData("timing")]
    [InlineData("timing group")]
    [InlineData("timing group leaf")]
    [InlineData("timing leaf")]
    [InlineData("timing-root")]
    [InlineData("version")]
    public void HelpTiming_ShouldIncludeRenderingAndWriting(string path)
    {
        var timeline = new CommandTimingRecorder();
        var renderer = new Mock<IHelpRenderer>(MockBehavior.Strict);
        var output = new Mock<TextWriter>(MockBehavior.Strict);
        var written = new List<string>();
        var renderCalls = 0;
        void AssertTimingOpen()
        {
            var measurement = Assert.Single(timeline.Capture().Measurements,
                row => row.Name == "Help render/output");
            Assert.False(measurement.IsComplete);
        }
        string[] Render()
        {
            AssertTimingOpen();
            renderCalls++;
            return ["first line", "second line"];
        }
        renderer.Setup(renderer => renderer.RenderRoot(It.IsAny<IEnumerable<RuntimeHelpGroup>>(), It.IsAny<uint?>()))
            .Returns(Render);
        renderer.Setup(renderer => renderer.RenderGroup(It.IsAny<RuntimeHelpGroup>(), It.IsAny<uint?>()))
            .Returns(Render);
        renderer.Setup(renderer => renderer.RenderCommand(It.IsAny<RuntimeHelpCommand>(), It.IsAny<uint?>()))
            .Returns(Render);
        output.Setup(output => output.WriteLine(It.IsAny<string>()))
            .Callback<string>(line =>
            {
                AssertTimingOpen();
                written.Add(line);
            });
        var info = MakeInfo("Test.Timing", ["timing leaf", "timing group leaf", "timing-root"]);
        var dispatcher = CreateDispatcher([info], output.Object, renderer: renderer.Object, performance: timeline);

        var exit = path.Length == 0 ? dispatcher.RenderRoot() : dispatcher.RenderExtensionHelp(path.Split(' '));

        Assert.Equal(0, exit);
        Assert.Equal(1, renderCalls);
        Assert.Equal(["first line", "second line"], written);
        Assert.True(Assert.Single(timeline.Capture().Measurements,
            row => row.Name == "Help render/output").IsComplete);
    }

    [Fact]
    public void ShouldBoldHeaders_ShouldSuppressAnsiForArbitraryWriter()
    {
        using var output = new StringWriter();

        Assert.False(RuntimeHelpDispatcher.ShouldBoldHeaders(output));
    }

    [Fact]
    public void ShouldBoldHeaders_ShouldUseOutputPolicyForConsoleOut()
    {
        var originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
        try
        {
            Environment.SetEnvironmentVariable("NO_COLOR", "1");

            Assert.False(RuntimeHelpDispatcher.ShouldBoldHeaders(Console.Out));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
        }
    }

    [Fact]
    public void ShouldBoldHeaders_ShouldHonorForceColorForArbitraryWriterOnlyThroughPolicyInput()
    {
        var originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
        var originalForceColor = Environment.GetEnvironmentVariable("FORCE_COLOR");
        try
        {
            Environment.SetEnvironmentVariable("NO_COLOR", null);
            Environment.SetEnvironmentVariable("FORCE_COLOR", "1");

            Assert.False(RuntimeHelpDispatcher.ShouldBoldHeaders(new StringWriter()));
            Assert.True(ColorPolicy.ShouldEmitColor(targetIsRedirected: true));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
            Environment.SetEnvironmentVariable("FORCE_COLOR", originalForceColor);
        }
    }

    [Fact]
    public void RenderExtensionHelp_ShouldCombineSharedGroupContributions_AndResolveEachCommand()
    {
        var storage = MakeInfo("A.Storage", ["storage account list", "other show"]);
        var blobs = MakeInfo("Z.StorageData.Blobs", ["STORAGE blob"]);
        using var output = new StringWriter();
        var dispatcher = CreateDispatcher([blobs, storage], output);

        Assert.Equal(0, dispatcher.RenderExtensionHelp(["storage"]));
        Assert.Contains("account", output.ToString());
        Assert.Contains("blob", output.ToString());
        Assert.DoesNotContain("other", output.ToString());

        foreach (var path in new[] { new[] { "storage", "account", "list" }, new[] { "storage", "blob" } })
        {
            output.GetStringBuilder().Clear();
            Assert.Equal(0, dispatcher.RenderExtensionHelp(path));
            Assert.Contains("# COMMAND", output.ToString());
            Assert.Contains($"az {string.Join(" ", path)}:", output.ToString());
        }
    }

    [Fact]
    public void TryResolveExtensionCore_ShouldPreferRootLeafOwner_WhenMultipleExtensionsShareVerb()
    {
        // Root-leaf owner sorts later by PackageId; without the root-leaf preference the wrong
        // extension would be chosen and `az config --help` would diverge from `az config` dispatch.
        var subOnly = MakeInfo("A.Sub", new[] { "config foo bar" });
        var rootOwner = MakeInfo("Z.Root", new[] { "config", "config show" });

        Assert.True(RuntimeHelpDispatcher.TryResolveExtensionCore(
            new[] { subOnly, rootOwner },
            "config",
            out var resolved));
        Assert.Equal("Z.Root", resolved.PackageId);
    }

    [Fact]
    public void TryResolveExtensionCore_ShouldTieBreakByPackageId_WhenNoRootLeafOwner()
    {
        var first = MakeInfo("A.Sub", new[] { "config foo" });
        var second = MakeInfo("Z.Sub", new[] { "config bar" });

        Assert.True(RuntimeHelpDispatcher.TryResolveExtensionCore(
            new[] { second, first },
            "config",
            out var resolved));
        Assert.Equal("A.Sub", resolved.PackageId);
    }

    [Fact]
    public void TryResolveExtensionCore_ShouldTieBreakByPackageId_WhenMultipleRootLeafOwners()
    {
        var first = MakeInfo("A.Root", new[] { "config", "config a" });
        var second = MakeInfo("Z.Root", new[] { "config", "config b" });

        Assert.True(RuntimeHelpDispatcher.TryResolveExtensionCore(
            new[] { second, first },
            "config",
            out var resolved));
        Assert.Equal("A.Root", resolved.PackageId);
    }

    [Fact]
    public void TryResolveExtensionCore_ShouldReturnFalse_WhenNoOwner()
    {
        var only = MakeInfo("A.Sub", new[] { "vm list" });

        Assert.False(RuntimeHelpDispatcher.TryResolveExtensionCore(
            new[] { only },
            "config",
            out _));
    }

    [Theory]
    [InlineData(typeof(TaggedRootProcessor), " [Preview,Experimental,Deprecated,GA]")]
    [InlineData(typeof(UntaggedRootProcessor), "")]
    [InlineData(typeof(object), "")]
    public void RenderRoot_ShouldDisplayRootCommandTags_WithoutChangingStandaloneHelp(Type processorType, string tagSuffix)
    {
        var info = MakeInfo("Test.Root", ["lifecycle-root"], processorType);
        using var output = new StringWriter();
        var dispatcher = CreateDispatcher([info], output);

        Assert.Equal(0, dispatcher.RenderRoot());
        Assert.Contains($"lifecycle-root{tagSuffix}", output.ToString().Split(Environment.NewLine));

        output.GetStringBuilder().Clear();

        Assert.Equal(0, dispatcher.RenderExtensionHelp(["lifecycle-root"]));
        var standaloneHelp = output.ToString();
        Assert.Contains("# COMMAND", standaloneHelp);
        Assert.Contains("az lifecycle-root:", standaloneHelp);
        Assert.DoesNotContain(" [", standaloneHelp);
    }

    [Fact]
    public void RenderRoot_ShouldNotLabelParentGroup_WhenOnlyChildCommandHasTags()
    {
        var info = MakeInfo("Test.Group", ["lifecycle-root lifecycle-root"], typeof(TaggedRootProcessor));
        using var output = new StringWriter();
        var dispatcher = CreateDispatcher([info], output);

        Assert.Equal(0, dispatcher.RenderRoot());
        Assert.Contains("lifecycle-root", output.ToString().Split(Environment.NewLine));
        Assert.DoesNotContain(" [", output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RenderRoot_ShouldNotLabelRoot_WhenChildrenMakeItNonRunnable(bool childrenInAnotherExtension)
    {
        var root = MakeInfo(
            "A.Root",
            childrenInAnotherExtension ? ["lifecycle-root"] : ["lifecycle-root", "lifecycle-root create"],
            typeof(TaggedRootProcessor));
        var child = MakeInfo("Z.Child", ["LIFECYCLE-ROOT create"]);
        using var output = new StringWriter();
        var dispatcher = CreateDispatcher(childrenInAnotherExtension ? [root, child] : [root], output);

        Assert.Equal(0, dispatcher.RenderRoot());
        Assert.Contains("lifecycle-root", output.ToString().Split(Environment.NewLine));
        Assert.DoesNotContain(" [", output.ToString());
    }

    [Fact]
    public void RenderRoot_ShouldPreferRootOwner_OverExtensionThatOnlyAdvertisesVerb()
    {
        var advertised = MakeInfo("A.Advertised", ["bar create"], verbs: ["lifecycle-root", "bar"]);
        var root = MakeInfo("Z.Root", ["lifecycle-root"], typeof(TaggedRootProcessor));
        using var output = new StringWriter();
        var dispatcher = CreateDispatcher([advertised, root], output);

        Assert.Equal(0, dispatcher.RenderRoot());
        Assert.Contains(
            "lifecycle-root [Preview,Experimental,Deprecated,GA]",
            output.ToString().Split(Environment.NewLine));
    }

    [Fact]
    public void RenderRoot_ShouldKeepHelpAvailableAndSuppressTags_WhenLeavesConflict()
    {
        var first = MakeInfo("A.Conflict", ["conflict-root"], typeof(TaggedRootProcessor));
        var second = MakeInfo("Z.Conflict", ["conflict-root"], typeof(TaggedRootProcessor));
        var other = MakeInfo("Test.Other", ["other-root"], typeof(TaggedRootProcessor));
        var debugMessages = new List<string>();
        using var output = new StringWriter();
        var dispatcher = CreateDispatcher([first, second, other], output, debugMessages);

        Assert.Equal(0, dispatcher.RenderRoot());
        var help = output.ToString();
        var lines = help.Split(Environment.NewLine);
        Assert.Contains("# GROUPS", lines);
        Assert.Single(lines, line => line == "conflict-root");
        Assert.Contains("other-root", lines);
        Assert.Contains("version", help);
        Assert.DoesNotContain(" [", help);
        Assert.Contains(debugMessages, message =>
            message.Contains("Duplicate leaf", StringComparison.Ordinal) &&
            message.Contains("suppress", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("Test.Shared")]
    [InlineData("test.shared")]
    public void RenderRoot_ShouldKeepHelpAvailableAndSuppressTags_WhenDiscoveryPackageIdsRepeat(string secondPackageId)
    {
        var first = MakeInfo("Test.Shared", ["first-root"], typeof(TaggedRootProcessor));
        var second = MakeInfo(secondPackageId, ["second-root"], typeof(TaggedRootProcessor));
        var debugMessages = new List<string>();
        using var output = new StringWriter();
        var dispatcher = CreateDispatcher([first, second], output, debugMessages);

        Assert.Equal(0, dispatcher.RenderRoot());
        var help = output.ToString();
        var lines = help.Split(Environment.NewLine);
        Assert.Contains("first-root", lines);
        Assert.Contains("second-root", lines);
        Assert.Contains("version", help);
        Assert.DoesNotContain(" [", help);
        Assert.Contains(debugMessages, message =>
            message.Contains("routing conflict", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("suppress", StringComparison.OrdinalIgnoreCase));
    }

    private static RuntimeHelpDispatcher CreateDispatcher(
        IReadOnlyList<DiscoveredExtensionInfo> infos,
        TextWriter output,
        List<string>? debugMessages = null,
        IHelpRenderer? renderer = null,
        CommandTimingRecorder? performance = null)
    {
        var engine = new Mock<IEngine>(MockBehavior.Strict);
        engine.Setup(engine => engine.OpenDiscoverySession(
                It.IsAny<IList<string>>(), null, It.IsAny<OutputStreams>(), true, It.IsAny<CancellationToken>()))
            .Returns((IList<string> _, string? _, OutputStreams streams, bool _, CancellationToken _) =>
            {
                var session = new Mock<ISession>();
                session.Setup(session => session.RunAsync())
                    .Returns(() => streams.WriteOutput(infos.ToList(), CancellationToken.None));
                return session.Object;
            });

        var streams = new OutputStreams(
            WriteOutput: (_, _) => Task.CompletedTask,
            WriteVerbose: (_, _) => Task.CompletedTask,
            WriteDebug: (message, _) =>
            {
                debugMessages?.Add($"{message}");
                return Task.CompletedTask;
            },
            WriteWarning: (_, _) => Task.CompletedTask,
            WriteError: (error, _) => throw new InvalidOperationException($"Unexpected error: {error}"),
            WriteInformation: (_, _) => Task.CompletedTask);
        return new RuntimeHelpDispatcher(engine.Object, ExtensionHintTable.Default, renderer ?? new RuntimeHelpRenderer(), output, streams)
        {
            SelectedPackageIds = () => infos.Select(info => info.PackageId).ToArray(),
            Performance = performance,
        };
    }

    private static DiscoveredExtensionInfo MakeInfo(
        string packageId,
        IEnumerable<string> commandKeys,
        Type? processorType = null,
        IReadOnlyList<string>? verbs = null)
    {
        var commands = commandKeys.ToDictionary(
            static k => k,
            k => new CommandMetadata(
                Name: k,
                InvocationName: k,
                Aliases: [],
                FullName: k,
                AssemblyName: string.Empty,
                ProcessorType: processorType ?? typeof(object),
                Parameters: new Dictionary<string, ParameterMetadata>(),
                ParameterAliases: new Dictionary<string, string>()),
            StringComparer.OrdinalIgnoreCase);

        return new DiscoveredExtensionInfo(
            PackageId: packageId,
            ResolvedVersion: "1.0.0",
            Verbs: verbs ?? [],
            Commands: commands,
            Location: null);
    }

    [ProcessorInfo(Tags = [HelpTag.Preview, HelpTag.Experimental, HelpTag.Deprecated, HelpTag.GA])]
    private sealed class TaggedRootProcessor { }

    [ProcessorInfo]
    private sealed class UntaggedRootProcessor { }
}
