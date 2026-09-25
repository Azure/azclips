// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Help;
using Core.Extensions;
using Moq;

namespace Cli.Tests.Recovery;

public class TroubleshootHelpTests
{
    [Theory]
    [InlineData(new[] { "TROUBLESHOOT" }, "troubleshoot")]
    [InlineData(new[] { "troubleshoot", "SHOW" }, "troubleshoot show")]
    public void Help_UsesIntrinsicIdentityWithoutResolvingAnExtension(string[] path, string expected)
    {
        var engine = new Mock<IEngine>(MockBehavior.Strict);
        var registry = new Mock<IExtensionRegistryStore>(MockBehavior.Strict);
        using var output = new StringWriter();
        var dispatcher = new RuntimeHelpDispatcher(engine.Object, null!, registry.Object, output);

        Assert.Equal(0, dispatcher.RenderExtensionHelp(path));
        Assert.Contains($"az {expected}", output.ToString());
        engine.VerifyNoOtherCalls();
        registry.VerifyNoOtherCalls();
    }

    [Fact]
    public void Help_RejectsUnknownIntrinsicChildrenWithoutExtensionFallback()
    {
        var engine = new Mock<IEngine>(MockBehavior.Strict);
        var registry = new Mock<IExtensionRegistryStore>(MockBehavior.Strict);
        using var output = new StringWriter();
        var dispatcher = new RuntimeHelpDispatcher(engine.Object, null!, registry.Object, output);
        Assert.Throws<UsageErrorException>(() => dispatcher.RenderExtensionHelp(["troubleshoot", "unknown"]));
        engine.VerifyNoOtherCalls();
        registry.VerifyNoOtherCalls();
    }

    [Fact]
    public void Help_RootAndShowShareTheAuthoritativeParameterMetadata()
    {
        var root = IntrinsicVerbs.CreateHelp(["troubleshoot"]);
        var show = IntrinsicVerbs.CreateHelp(["troubleshoot", "show"]);
        Assert.Equal(["id", "list", "style"], root.Parameters.Select(parameter => parameter.Name));
        Assert.Equal(["oneshot", "interactive"], root.Parameters.Single(parameter => parameter.Name == "style").AllowedValues);
        var id = Assert.Single(show.Parameters);
        Assert.Equal("id", id.Name);
        Assert.True(id.Required);
        Assert.False(IntrinsicVerbs.TryResolve("--troubleshoot", out _));
    }
}
