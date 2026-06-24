// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Help;
using Microsoft.Azure.Cirrus.Extensions.Login;
using Tests.Common.Utilities;

namespace Cli.Tests;

public class RuntimeHelpRendererTests
{
    [Fact]
    public void RenderGroup_ShouldMatchFixture_ForLoginGroup()
    {
        var renderer = new RuntimeHelpRenderer();
        var helpProvider = new ExtensionIndexHelpAdapter(new ExtensionIndex(), "Login");
        var group = helpProvider.GetGroupHelp(["login"]);

        Assert.NotNull(group);

        var actual = string.Join(Environment.NewLine, renderer.RenderGroup(group!, terminalWidth: null));
        var expected = LoadFixture("Login.RuntimeGroupHelp.txt");

        Assert.Equal(NormalizeLineEndings(expected), NormalizeLineEndings(actual));
    }

    [Fact]
    public void RenderCommand_ShouldMatchFixture_ForLoginShowCommand()
    {
        var renderer = new RuntimeHelpRenderer();
        var helpProvider = new ExtensionIndexHelpAdapter(new ExtensionIndex(), "Login");
        var command = helpProvider.GetCommandHelp(["login", "show"]);

        Assert.NotNull(command);

        var actual = string.Join(Environment.NewLine, renderer.RenderCommand(command!, terminalWidth: null));
        var expected = LoadFixture("Login.RuntimeCommandHelp.show.txt");

        Assert.Equal(NormalizeLineEndings(expected), NormalizeLineEndings(actual));
    }

    [Fact]
    public void RenderRoot_ShouldMatchFixture_ForSingleLoginGroup()
    {
        var renderer = new RuntimeHelpRenderer();
        var groups = new RuntimeHelpGroup[]
        {
            new(
                GroupPath: "login",
                Summary: "Authenticate to Azure.",
                Subgroups: [],
                Commands: [])
        };

        var actual = string.Join(Environment.NewLine, renderer.RenderRoot(groups, terminalWidth: null));
        var expected = LoadFixture("Root.RuntimeHelp.txt");

        Assert.Equal(NormalizeLineEndings(expected), NormalizeLineEndings(actual));
    }

    [Fact]
    public void RenderGroup_ShouldRenderSubgroupsSection_WhenSubgroupsArePresent()
    {
        var renderer = new RuntimeHelpRenderer();
        var group = new RuntimeHelpGroup(
            GroupPath: "group",
            Summary: "Manage Azure resource groups.",
            Subgroups:
            [
                new RuntimeHelpGroupSummary("role", "Manage role assignments."),
                new RuntimeHelpGroupSummary("tag", string.Empty),
            ],
            Commands:
            [
                new RuntimeHelpCommandSummary("create", "Create a resource group.", []),
            ]);

        var actual = string.Join("\n", renderer.RenderGroup(group, terminalWidth: null));

        Assert.Contains("Subgroups:", actual);
        Assert.Contains("role : Manage role assignments.", actual);
        Assert.Contains("tag", actual); // empty summary still listed
        Assert.Contains("Commands:", actual);
        Assert.Contains("create", actual);
    }

    [Fact]
    public void RenderGroup_ShouldOmitSubgroupsSection_WhenSubgroupsAreEmpty()
    {
        var renderer = new RuntimeHelpRenderer();
        var group = new RuntimeHelpGroup(
            GroupPath: "login",
            Summary: "Authenticate.",
            Subgroups: [],
            Commands: [new RuntimeHelpCommandSummary("login", "Sign in.", [])]);

        var actual = string.Join("\n", renderer.RenderGroup(group, terminalWidth: null));

        Assert.DoesNotContain("Subgroups:", actual);
    }

    private static string LoadFixture(string fileName)
    {
        var repoRoot = RepoLocator.GetRepoRoot();
        var path = Path.Combine(repoRoot, "tests", "Cli.Tests", "Fixtures", "Help", fileName);
        return File.ReadAllText(path).TrimEnd();
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd();
    }
}
