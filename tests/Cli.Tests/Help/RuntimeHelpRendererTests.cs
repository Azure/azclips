// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Help;
using Microsoft.Azure.Azclips.Extensions.Accounts;
using Tests.Common.Utilities;

namespace Cli.Tests;

public class RuntimeHelpRendererTests
{
    [Fact]
    public void RenderGroup_ShouldMatchFixture_ForLoginGroup()
    {
        var renderer = new RuntimeHelpRenderer();
        var helpProvider = new ExtensionIndexHelpAdapter(new ExtensionIndex().GetCommandTable(ClientType.CLI), "login");
        var group = helpProvider.GetGroupHelp(["login"]);

        Assert.NotNull(group);

        var actual = string.Join(Environment.NewLine, renderer.RenderGroup(group!, terminalWidth: null));
        var expected = LoadFixture("Login.RuntimeGroupHelp.txt");

        Assert.Equal(NormalizeLineEndings(expected), NormalizeLineEndings(actual));
    }

    [Fact]
    public void RenderCommand_ShouldMatchFixture_ForLogoutCommand()
    {
        var renderer = new RuntimeHelpRenderer();
        var helpProvider = new ExtensionIndexHelpAdapter(new ExtensionIndex().GetCommandTable(ClientType.CLI), "logout");
        var command = helpProvider.GetCommandHelp(["logout"]);

        Assert.NotNull(command);

        var actual = string.Join(Environment.NewLine, renderer.RenderCommand(command!, terminalWidth: null));
        var expected = LoadFixture("Login.RuntimeCommandHelp.logout.txt");

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

        Assert.Contains("# SUBGROUPS", actual);
        Assert.Contains("role:\nManage role assignments.", actual);
        Assert.Contains("tag", actual);
        Assert.DoesNotContain("tag:", actual); // empty summary must not get a trailing colon
        Assert.Contains("# COMMANDS", actual);
        Assert.Contains("create:\nCreate a resource group.", actual);
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

        Assert.DoesNotContain("# SUBGROUPS", actual);
    }

    [Fact]
    public void RenderCommand_ShouldRenderParametersUnderTheirGroupHeaders_WhenGroupNameIsSet()
    {
        var renderer = new RuntimeHelpRenderer();
        var command = new RuntimeHelpCommand(
            CommandPath: "vm create",
            Summary: "Create a VM.",
            Description: string.Empty,
            Parameters:
            [
                new RuntimeHelpParameter("name", ["n"], true, "VM name.", "VM name.", [], "(All)", false, false, GroupName: null),
                new RuntimeHelpParameter("admin-username", [], true, "Admin user.", "Admin user.", [], "(All)", false, false, GroupName: "Authentication Arguments"),
                new RuntimeHelpParameter("vnet-name", [], true, "VNet.", "VNet.", [], "(All)", false, false, GroupName: "Network Arguments"),
                new RuntimeHelpParameter("admin-password", [], true, "Admin pass.", "Admin pass.", [], "(All)", false, false, GroupName: "Authentication Arguments"),
            ],
            Examples: [],
            Tags: []);

        var actual = string.Join("\n", renderer.RenderCommand(command, terminalWidth: null));

        Assert.Contains("# ARGUMENTS\n--name", actual);
        Assert.Contains("# AUTHENTICATION ARGUMENTS\n--admin-username", actual);
        Assert.Contains("--admin-password", actual);
        Assert.Contains("# NETWORK ARGUMENTS\n--vnet-name", actual);
    }

    [Fact]
    public void RenderCommand_ShouldSortNamedGroupsAlphabeticallyAfterArguments()
    {
        var renderer = new RuntimeHelpRenderer();
        var command = new RuntimeHelpCommand(
            CommandPath: "vm create",
            Summary: "Create a VM.",
            Description: string.Empty,
            Parameters:
            [
                new RuntimeHelpParameter("s", [], false, ".", ".", [], "(All)", false, false, GroupName: "Storage Arguments"),
                new RuntimeHelpParameter("a", [], false, ".", ".", [], "(All)", false, false, GroupName: "Authentication Arguments"),
                new RuntimeHelpParameter("n", [], false, ".", ".", [], "(All)", false, false, GroupName: "Network Arguments"),
            ],
            Examples: [],
            Tags: []);

        var actual = string.Join("\n", renderer.RenderCommand(command, terminalWidth: null));

        var auth = actual.IndexOf("# AUTHENTICATION ARGUMENTS", StringComparison.Ordinal);
        var net = actual.IndexOf("# NETWORK ARGUMENTS", StringComparison.Ordinal);
        var storage = actual.IndexOf("# STORAGE ARGUMENTS", StringComparison.Ordinal);
        var global = actual.IndexOf("# GLOBAL ARGUMENTS", StringComparison.Ordinal);

        Assert.True(auth > 0 && net > auth && storage > net && global > storage,
            $"Expected Auth < Net < Storage < Global, got Auth={auth} Net={net} Storage={storage} Global={global}");
    }

    [Fact]
    public void RenderCommand_ShouldMatchLegacyLayout_WhenAllParametersAreUngrouped()
    {
        var renderer = new RuntimeHelpRenderer();
        var command = new RuntimeHelpCommand(
            CommandPath: "logout",
            Summary: "Sign out.",
            Description: string.Empty,
            Parameters:
            [
                new RuntimeHelpParameter("verbose", [], false, "Verbose.", "Verbose.", [], "(All)", false, false, GroupName: null),
            ],
            Examples: [],
            Tags: []);

        var actual = string.Join("\n", renderer.RenderCommand(command, terminalWidth: null));

        Assert.Contains("# ARGUMENTS\n--verbose", actual);
        // No extra headers between "# ARGUMENTS" and "# GLOBAL ARGUMENTS" when nothing is grouped.
        var argsBlockEnd = actual.IndexOf("\n# GLOBAL ARGUMENTS", StringComparison.Ordinal);
        var argsBlock = actual[..argsBlockEnd];
        Assert.DoesNotContain("# ARGUMENTS\n\n", argsBlock);
    }

    [Fact]
    public void RenderCommand_ShouldOmitArgumentsHeader_WhenEveryParameterIsGrouped()
    {
        var renderer = new RuntimeHelpRenderer();
        var command = new RuntimeHelpCommand(
            CommandPath: "vm create",
            Summary: "Create a VM.",
            Description: string.Empty,
            Parameters:
            [
                new RuntimeHelpParameter("admin-username", [], true, "Admin user.", "Admin user.", [], "(All)", false, false, GroupName: "Authentication Arguments"),
            ],
            Examples: [],
            Tags: []);

        var actual = string.Join("\n", renderer.RenderCommand(command, terminalWidth: null));

        // The default "# ARGUMENTS" header must not fire when there are no ungrouped params.
        // "# ARGUMENTS" is not a substring of "# AUTHENTICATION ARGUMENTS" / "# GLOBAL ARGUMENTS", so an exact check is sufficient.
        Assert.DoesNotContain("# ARGUMENTS", actual);
        Assert.Contains("# AUTHENTICATION ARGUMENTS\n--admin-username", actual);
    }

    [Fact]
    public void RenderCommand_ShouldEmitValuesFromLine_WhenValueSourcesIsSet()
    {
        var renderer = new RuntimeHelpRenderer();
        var command = new RuntimeHelpCommand(
            CommandPath: "vm create",
            Summary: "Create a VM.",
            Description: string.Empty,
            Parameters:
            [
                new RuntimeHelpParameter("resource-group", [], true, "The resource group name.", "The resource group name.", [], "(All)", false, false, GroupName: null, ValueSources: "az group list"),
            ],
            Examples: [],
            Tags: []);

        var actual = string.Join("\n", renderer.RenderCommand(command, terminalWidth: null));

        Assert.Contains("--resource-group [Required]:\nThe resource group name.\nValues from: az group list.", actual);
    }

    [Fact]
    public void RenderCommand_ShouldNotEmitValuesFromLine_WhenValueSourcesIsNull()
    {
        var renderer = new RuntimeHelpRenderer();
        var command = new RuntimeHelpCommand(
            CommandPath: "vm create",
            Summary: "Create a VM.",
            Description: string.Empty,
            Parameters:
            [
                new RuntimeHelpParameter("name", [], true, "VM name.", "VM name.", [], "(All)", false, false, GroupName: null, ValueSources: null),
            ],
            Examples: [],
            Tags: []);

        var actual = string.Join("\n", renderer.RenderCommand(command, terminalWidth: null));

        Assert.DoesNotContain("Values from:", actual);
    }

    [Fact]
    public void RenderCommand_ShouldNumberExamples_InOrderWithBlankLineSeparation()
    {
        var renderer = new RuntimeHelpRenderer();
        var command = new RuntimeHelpCommand(
            CommandPath: "vm create",
            Summary: "Create a VM.",
            Description: string.Empty,
            Parameters: [],
            Examples:
            [
                new RuntimeHelpExample("First example", "az vm create --name vm1"),
                new RuntimeHelpExample("Second example", "az vm create --name vm2"),
            ],
            Tags: []);

        var actual = string.Join("\n", renderer.RenderCommand(command, terminalWidth: null));

        Assert.Contains("1. First example\naz vm create --name vm1\n\n2. Second example\naz vm create --name vm2", actual);
    }

    [Fact]
    public void RenderCommand_ShouldNotEmitEscapeCodes_WhenBoldHeadersIsFalse()
    {
        var renderer = new RuntimeHelpRenderer(boldHeaders: false);
        var command = new RuntimeHelpCommand(
            CommandPath: "vm create",
            Summary: "Create a VM.",
            Description: string.Empty,
            Parameters: [],
            Examples: [],
            Tags: []);

        var actual = string.Join("\n", renderer.RenderCommand(command, terminalWidth: null));

        Assert.Contains("\n# COMMAND\n", "\n" + actual + "\n");
        Assert.DoesNotContain("\u001b[", actual);
    }

    [Fact]
    public void RenderCommand_ShouldWrapSectionHeaders_WhenBoldHeadersIsTrue()
    {
        var renderer = new RuntimeHelpRenderer(boldHeaders: true);
        var command = new RuntimeHelpCommand(
            CommandPath: "vm create",
            Summary: "Create a VM.",
            Description: string.Empty,
            Parameters: [],
            Examples: [],
            Tags: []);

        var actual = string.Join("\n", renderer.RenderCommand(command, terminalWidth: null));

        Assert.Contains("\u001b[1m# COMMAND\u001b[0m", actual);
        Assert.Contains("\u001b[1m# ARGUMENTS\u001b[0m", actual);
        Assert.Contains("\u001b[1m# GLOBAL ARGUMENTS\u001b[0m", actual);
        // Only the header text itself is wrapped, not the name/summary line beneath it.
        Assert.DoesNotContain("\u001b[1maz vm create", actual);
    }

    [Fact]
    public void RenderGroupsSection_ShouldSortGroupsAndHaveNoExteriorBlankLines()
    {
        var renderer = new RuntimeHelpRenderer();
        RuntimeHelpGroup[] groups =
        [
            new("vm", "Manage VMs.", [], []),
            new("group", "Manage groups.", [], []),
        ];

        var lines = renderer.RenderGroupsSection(groups, terminalWidth: null);

        Assert.Equal("# GROUPS", lines[0]);
        Assert.Equal("group:", lines[1]);
        Assert.Equal("vm:", lines[4]);
        Assert.NotEqual(string.Empty, lines[0]);
        Assert.NotEqual(string.Empty, lines[^1]);
    }

    [Fact]
    public void RenderIdentitySections_ShouldRenderHeadingsAndSummaries()
    {
        var renderer = new RuntimeHelpRenderer();
        var group = new RuntimeHelpGroup("vm", "Manage VMs.", [], []);
        var command = Command(parameters: [], examples: []);

        Assert.Equal(["# GROUP", "az vm:", "Manage VMs."], renderer.RenderGroupIdentitySection(group, null));
        Assert.Equal(["# COMMAND", "az vm create:", "Create a VM."], renderer.RenderCommandIdentitySection(command, null));
    }

    [Fact]
    public void RenderSubgroupsSection_ShouldBeOptionalAndRenderEntries()
    {
        var renderer = new RuntimeHelpRenderer();

        Assert.Empty(renderer.RenderSubgroupsSection([], null));
        Assert.Equal(
            ["# SUBGROUPS", "role:", "Manage roles.", "", "tag"],
            renderer.RenderSubgroupsSection(
                [new("role", "Manage roles."), new("tag", string.Empty)],
                null));
    }

    [Fact]
    public void RenderCommandsSection_ShouldRenderEmptyMarkerAndTags()
    {
        var renderer = new RuntimeHelpRenderer();

        Assert.Equal(["# COMMANDS", "(none)"], renderer.RenderCommandsSection([], null));
        Assert.Equal(
            ["# COMMANDS", "create [Preview]:", "Create a VM."],
            renderer.RenderCommandsSection(
                [new("create", "Create a VM.", [HelpTag.Preview])],
                null));
    }

    [Fact]
    public void RenderParameterSections_ShouldPreserveGroupingAndFormatting()
    {
        var renderer = new RuntimeHelpRenderer();
        RuntimeHelpParameter[] parameters =
        [
            new("name", ["n"], true, "VM name.", "VM name.", [], "(All)", false, false, ValueSources: "az vm list"),
            new("vnet", [], false, "VNet.", "VNet.", [], "(All)", false, false, GroupName: "Network Arguments"),
            new("user", [], false, "User.", "User.", [], "(All)", false, false, GroupName: "Authentication Arguments"),
        ];

        var actual = string.Join("\n", renderer.RenderParameterSections(parameters, null));

        Assert.StartsWith("# ARGUMENTS\n--name -n [Required]:\nVM name.\nValues from: az vm list.", actual);
        Assert.True(actual.IndexOf("# AUTHENTICATION ARGUMENTS", StringComparison.Ordinal)
            < actual.IndexOf("# NETWORK ARGUMENTS", StringComparison.Ordinal));
        Assert.False(actual.StartsWith('\n'));
        Assert.False(actual.EndsWith('\n'));
    }

    [Fact]
    public void RenderParameterGroupSection_ShouldRenderOneNamedGroupIndependently()
    {
        var renderer = new RuntimeHelpRenderer();
        RuntimeHelpParameter[] parameters =
        [
            new("admin-username", ["u"], true, "Admin user.", "Admin user.", [], "(All)", false, false),
        ];

        var lines = renderer.RenderParameterGroupSection("Authentication Arguments", parameters, null);

        Assert.Equal(
            ["# AUTHENTICATION ARGUMENTS", "--admin-username -u [Required]:", "Admin user."],
            lines);
    }

    [Fact]
    public void RenderExamplesAndGlobalArgumentsSections_ShouldRenderIndependently()
    {
        var renderer = new RuntimeHelpRenderer();

        Assert.Empty(renderer.RenderExamplesSection([], null));
        Assert.Equal(
            ["# EXAMPLES", "1. Create", "az vm create", "", "2. List", "az vm list"],
            renderer.RenderExamplesSection(
                [new("Create", "az vm create"), new("List", "az vm list")],
                null));
        Assert.Equal("# GLOBAL ARGUMENTS", renderer.RenderGlobalArgumentsSection(null)[0]);
    }

    [Fact]
    public void RenderCommand_ShouldEqualCompositionOfItsSections()
    {
        var renderer = new RuntimeHelpRenderer();
        var command = Command(
            parameters: [new("name", [], true, "VM name.", "VM name.", [], "(All)", false, false)],
            examples: [new("Create", "az vm create --name vm1")]);

        var expected = Compose(
            renderer.RenderCommandIdentitySection(command, null),
            renderer.RenderParameterSections(command.Parameters, null),
            renderer.RenderExamplesSection(command.Examples, null),
            renderer.RenderGlobalArgumentsSection(null));

        Assert.Equal(expected, renderer.RenderCommand(command, null));
    }

    [Fact]
    public void RenderStandaloneSection_ShouldHonorBoldHeaders()
    {
        var renderer = new RuntimeHelpRenderer(boldHeaders: true);

        var lines = renderer.RenderExamplesSection([new("Create", "az vm create")], null);

        Assert.Equal("\u001b[1m# EXAMPLES\u001b[0m", lines[0]);
        Assert.DoesNotContain("\u001b[", lines[1]);
        Assert.DoesNotContain("\u001b[", lines[2]);
    }

    private static RuntimeHelpCommand Command(
        IReadOnlyList<RuntimeHelpParameter> parameters,
        IReadOnlyList<RuntimeHelpExample> examples)
        => new("vm create", "Create a VM.", string.Empty, parameters, examples, []);

    private static string[] Compose(params string[][] sections)
    {
        var lines = new List<string>();
        foreach (var section in sections.Where(static section => section.Length > 0))
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.AddRange(section);
        }

        return [.. lines];
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
