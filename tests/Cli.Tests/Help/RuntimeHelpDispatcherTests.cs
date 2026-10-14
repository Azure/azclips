// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Cli.Help;
using Core.Extensions;
using Core.Runtime;
using Moq;
using ComputeExtensionIndex = Microsoft.Azure.Azclips.Extensions.Compute.ExtensionIndex;
using VMCreateProcessor = Microsoft.Azure.Azclips.Extensions.Compute.Processors.NewVMProcessor_Orchestrator;

namespace Cli.Tests;

[Collection("Azclips Service Provider Tests")]
public class RuntimeHelpDispatcherTests
{
    [Fact]
    public void ShouldBoldHeaders_ShouldSuppressAnsiForArbitraryWriter()
    {
        using var output = new StringWriter();

        Assert.False(RuntimeHelpDispatcher.ShouldBoldHeaders(output));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldBoldHeaders_ShouldUseTargetPolicyForConsoleStreams(bool useStderr)
    {
        var originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
        try
        {
            Environment.SetEnvironmentVariable("NO_COLOR", "1");

            Assert.False(RuntimeHelpDispatcher.ShouldBoldHeaders(useStderr ? Console.Error : Console.Out));
            Assert.False(RuntimeHelpDispatcher.ShouldBoldHeaders(null));
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

            using var output = new StringWriter();
            Assert.False(RuntimeHelpDispatcher.ShouldBoldHeaders(output));
            Assert.True(ColorPolicy.ShouldEmitColor(targetIsRedirected: true));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
            Environment.SetEnvironmentVariable("FORCE_COLOR", originalForceColor);
        }
    }

    [Theory]
    [InlineData("default", ColorMode.Always, null, null, null, true)]
    [InlineData("stdout", ColorMode.Always, null, null, null, true)]
    [InlineData("stderr", ColorMode.Always, null, null, null, true)]
    [InlineData("writer", ColorMode.Always, null, null, null, false)]
    [InlineData("default", ColorMode.Never, null, "1", "dumb", true)]
    [InlineData("stdout", ColorMode.Never, null, "1", "dumb", true)]
    [InlineData("stderr", ColorMode.Never, null, "1", "dumb", true)]
    [InlineData("writer", ColorMode.Never, null, "1", "dumb", false)]
    [InlineData("default", ColorMode.Always, "1", "1", null, false)]
    [InlineData("stdout", ColorMode.Always, "1", "1", null, false)]
    [InlineData("stderr", ColorMode.Always, "1", "1", null, false)]
    [InlineData("writer", ColorMode.Always, "1", "1", null, false)]
    [InlineData("default", ColorMode.Never, null, null, null, false)]
    [InlineData("stdout", ColorMode.Never, null, null, null, false)]
    [InlineData("stderr", ColorMode.Never, null, null, null, false)]
    [InlineData("writer", ColorMode.Never, null, null, null, false)]
    [InlineData("default", ColorMode.Auto, null, null, "dumb", false)]
    [InlineData("stdout", ColorMode.Auto, null, null, "dumb", false)]
    [InlineData("stderr", ColorMode.Auto, null, null, "dumb", false)]
    [InlineData("writer", ColorMode.Auto, null, null, "dumb", false)]
    [InlineData("default", ColorMode.Auto, null, null, null, null)]
    [InlineData("stdout", ColorMode.Auto, null, null, null, null)]
    [InlineData("stderr", ColorMode.Auto, null, null, null, null)]
    [InlineData("writer", ColorMode.Auto, null, null, null, false)]
    public void RenderExtensionHelp_PublicConstructor_ShouldApplyStylingPolicyToVmCreate(
        string destination,
        ColorMode mode,
        string? noColor,
        string? forceColor,
        string? term,
        bool? expectedBold)
    {
        var commandTable = new ComputeExtensionIndex().GetCommandTable(ClientType.CLI);
        Assert.Equal(typeof(VMCreateProcessor), commandTable["vm create"].ProcessorType);
        var command = new ExtensionIndexHelpAdapter(commandTable, "vm").GetCommandHelp(["vm", "create"]);
        Assert.NotNull(command);

        var output = CaptureVmCreateHelp(commandTable, destination, mode, noColor, forceColor, term);
        var bold = expectedBold ?? !(destination == "stderr" ? Console.IsErrorRedirected : Console.IsOutputRedirected);
        var plainLines = new RuntimeHelpRenderer().RenderCommand(command, terminalWidth: null);
        var expectedLines = plainLines.Select(line =>
            bold && line.StartsWith("# ", StringComparison.Ordinal)
                ? $"\u001b[1m{line}\u001b[0m"
                : line);

        Assert.Equal(string.Join(Environment.NewLine, expectedLines) + Environment.NewLine, output);
        Assert.Contains("# ARGUMENTS", output);
        Assert.Contains("# AUTHENTICATION ARGUMENTS", output);
        Assert.Contains("# NETWORK ARGUMENTS", output);
        Assert.Contains("# SECURITY ARGUMENTS", output);
        Assert.Contains("# GLOBAL ARGUMENTS", output);
        if (!bold)
        {
            Assert.DoesNotContain('\u001b', output);
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

    private static string CaptureVmCreateHelp(
        IReadOnlyDictionary<string, CommandMetadata> commandTable,
        string destination,
        ColorMode mode,
        string? noColor,
        string? forceColor,
        string? term)
    {
        var engine = new Mock<IEngine>(MockBehavior.Strict);
        var session = new Mock<ISession>(MockBehavior.Strict);
        var registryStore = new Mock<IExtensionRegistryStore>(MockBehavior.Strict);
        OutputStreamDelegate noop = (_, _) => Task.CompletedTask;
        var streams = new OutputStreams(noop, noop, noop, noop, noop, noop);
        var discovered = new List<DiscoveredExtensionInfo>
        {
            new("Microsoft.Azure.Azclips.Extensions.Compute", "1.0.0", ["vm", "disk"], commandTable, null),
        };
        engine.Setup(e => e.OpenDiscoverySession(
                It.Is<IList<string>>(names => names.Count == 0),
                null,
                It.IsAny<OutputStreams>(),
                true,
                CancellationToken.None))
            .Returns((IList<string> _, string? _, OutputStreams discoveryStreams, bool _, CancellationToken _) =>
            {
                session.Setup(s => s.RunAsync())
                    .Returns(() => discoveryStreams.WriteOutput(discovered, CancellationToken.None));
                return session.Object;
            });
        session.Setup(s => s.Dispose());
        registryStore.Setup(store => store.Load()).Returns(new ExtensionRegistry());

        var originalOut = Console.Out;
        var originalError = Console.Error;
        var originalMode = ColorPolicy.CurrentMode;
        var originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
        var originalForceColor = Environment.GetEnvironmentVariable("FORCE_COLOR");
        var originalTerm = Environment.GetEnvironmentVariable("TERM");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        using var writer = new StringWriter();
        try
        {
            // Console capture and the existing policy override exercise the public constructor
            // without pretending that Console.SetOut changes OS-level redirection detection.
            Console.SetOut(stdout);
            Console.SetError(stderr);
            ColorPolicy.Initialize(mode);
            Environment.SetEnvironmentVariable("NO_COLOR", noColor);
            Environment.SetEnvironmentVariable("FORCE_COLOR", forceColor);
            Environment.SetEnvironmentVariable("TERM", term);

            var dispatcher = destination switch
            {
                "default" => new RuntimeHelpDispatcher(engine.Object, streams, registryStore.Object),
                "stdout" => new RuntimeHelpDispatcher(engine.Object, streams, registryStore.Object, Console.Out),
                "stderr" => new RuntimeHelpDispatcher(engine.Object, streams, registryStore.Object, Console.Error),
                "writer" => new RuntimeHelpDispatcher(engine.Object, streams, registryStore.Object, writer),
                _ => throw new ArgumentException("Unknown help output destination.", nameof(destination)),
            };
            Assert.Equal(0, dispatcher.RenderExtensionHelp(["vm", "create"]));
            engine.VerifyAll();
            engine.VerifyNoOtherCalls();
            session.VerifyAll();
            session.VerifyNoOtherCalls();
            registryStore.VerifyAll();
            registryStore.VerifyNoOtherCalls();

            Assert.Equal(string.Empty, destination is "default" or "stdout" ? stderr.ToString() : stdout.ToString());
            if (destination == "writer")
            {
                Assert.Equal(string.Empty, stderr.ToString());
            }

            return destination switch
            {
                "stderr" => stderr.ToString(),
                "writer" => writer.ToString(),
                _ => stdout.ToString(),
            };
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            ColorPolicy.Initialize(originalMode);
            Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
            Environment.SetEnvironmentVariable("FORCE_COLOR", originalForceColor);
            Environment.SetEnvironmentVariable("TERM", originalTerm);
        }
    }

    private static DiscoveredExtensionInfo MakeInfo(string packageId, IEnumerable<string> commandKeys)
    {
        var commands = commandKeys.ToDictionary(
            static k => k,
            static k => new CommandMetadata(
                Name: k,
                InvocationName: k,
                Aliases: [],
                FullName: k,
                AssemblyName: string.Empty,
                ProcessorType: typeof(object),
                Parameters: new Dictionary<string, ParameterMetadata>(),
                ParameterAliases: new Dictionary<string, string>()),
            StringComparer.OrdinalIgnoreCase);

        return new DiscoveredExtensionInfo(
            PackageId: packageId,
            ResolvedVersion: "1.0.0",
            Verbs: Array.Empty<string>(),
            Commands: commands,
            Location: null);
    }
}
