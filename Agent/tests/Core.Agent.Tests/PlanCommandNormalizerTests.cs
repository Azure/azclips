using Core.Agent.Hosting;
using Core.Agent.Plan;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for <see cref="PlanCommandNormalizer"/> — restoring the missing
/// <c>az</c> on plan steps that start with a full command name from the help
/// catalog, while leaving az commands, prose and other programs alone.
/// </summary>
public class PlanCommandNormalizerTests
{
    // Command names as `az agent tool help` lists them (cliName).
    private static readonly string[] CommandNames =
    [
        "deployment group create", "deployment group show", "login", "network vnet create",
        "network vnet subnet create", "rest", "vm create", "vm show",
    ];

    private static IReadOnlyList<ConsoleApprovalPrompt.PlanStep> Steps(params string[] commands) =>
        commands.Select((command, index) => new ConsoleApprovalPrompt.PlanStep(index + 1, $"step {index + 1}", command)).ToList();

    [Theory]
    [InlineData("rest --method GET --uri https://example", "az rest --method GET --uri https://example")]
    [InlineData("  deployment group create --resource-group g --template-file main.bicep", "az deployment group create --resource-group g --template-file main.bicep")]
    [InlineData("network vnet subnet create --name s", "az network vnet subnet create --name s")]
    [InlineData("vm show", "az vm show")]
    public void Adds_az_to_catalog_commands(string command, string expected)
    {
        var result = PlanCommandNormalizer.EnsureAzPrefix(Steps(command), CommandNames);

        Assert.Equal(expected, Assert.Single(result!).Cmd);
    }

    [Theory]
    [InlineData("az vm show --name x")]
    [InlineData("AZ vm show --name x")]
    [InlineData("kubectl get pods")]
    [InlineData("ssh-keygen -t rsa")]
    [InlineData("New-AzVM -Name x")]
    [InlineData("--help")]
    // Prose the model put in a step, written with a capital and a small first letter.
    [InlineData("Use a strong password value for `--admin-password` that meets Azure VM password requirements.")]
    [InlineData("use a strong password value for --admin-password")]
    // Starts with a command group's word, but not with a full command name.
    [InlineData("network security group must allow port 22")]
    [InlineData("deployment group --name d")]
    // Command names are lowercase; a capitalized word is prose.
    [InlineData("Login with an account that can read the subscription")]
    [InlineData("deploment group create --name d")]
    public void Leaves_az_commands_prose_and_other_programs_unchanged(string command)
    {
        var steps = Steps(command);

        var result = PlanCommandNormalizer.EnsureAzPrefix(steps, CommandNames);

        Assert.Same(steps, result);
    }

    [Fact]
    public void Matches_whole_words_and_takes_the_longest_command_name()
    {
        Assert.Equal("deployment group create",
            PlanCommandNormalizer.MatchCommandName("deployment group create --resource-group g", CommandNames));
        Assert.Equal("vm create", PlanCommandNormalizer.MatchCommandName("vm create --name x", ["vm", "vm create"]));
        Assert.Null(PlanCommandNormalizer.MatchCommandName("vm createx --name x", CommandNames));
        Assert.Null(PlanCommandNormalizer.MatchCommandName("vm --name x", CommandNames));
    }

    [Fact]
    public void Fixes_only_the_steps_that_need_it()
    {
        var result = PlanCommandNormalizer.EnsureAzPrefix(
            Steps("az group create -n g -l eastus", "rest --method GET", "kubectl get pods"), CommandNames);

        Assert.Equal(new[] { "az group create -n g -l eastus", "az rest --method GET", "kubectl get pods" },
            result!.Select(step => step.Cmd));
        Assert.Equal(new[] { 1, 2, 3 }, result!.Select(step => step.Id));
    }

    [Fact]
    public void Changes_nothing_without_command_names()
    {
        var steps = Steps("rest --method GET");

        Assert.Same(steps, PlanCommandNormalizer.EnsureAzPrefix(steps, []));
    }

    [Fact]
    public void Returns_null_for_no_steps() =>
        Assert.Null(PlanCommandNormalizer.EnsureAzPrefix(null, CommandNames));

    [Fact]
    public void Needs_the_catalog_only_when_a_step_does_not_start_with_az()
    {
        Assert.False(PlanCommandNormalizer.NeedsCatalog(Steps("az vm show", "AZ rest --method GET")));
        Assert.False(PlanCommandNormalizer.NeedsCatalog(Steps("--help")));
        Assert.False(PlanCommandNormalizer.NeedsCatalog(null));
        Assert.True(PlanCommandNormalizer.NeedsCatalog(Steps("az vm show", "kubectl get pods")));
    }

    [Fact]
    public void Reads_the_cli_command_names_from_the_catalog()
    {
        const string catalog = """
            {"groups":[{"verb":"vm","summary":"Manage virtual machines."}],
             "commands":[
               {"cliName":"vm create","psName":"New-AzVM","verb":"vm","summary":"Create a virtual machine."},
               {"cliName":"","psName":"Get-AzWidget","verb":"widget","summary":"A cmdlet without a CLI name."},
               {"cliName":"rest","psName":"","verb":"rest","summary":"Send an HTTP request."}]}
            """;

        Assert.Equal(new[] { "vm create", "rest" }, PlanCommandNormalizer.ReadCommandNames(catalog));
    }

    [Theory]
    [InlineData("{\"error\":\"shell_out_timeout\",\"timeoutSeconds\":30}")]
    [InlineData("[]")]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData(null)]
    public void Reads_no_command_names_from_an_error_or_other_output(string? output) =>
        Assert.Null(PlanCommandNormalizer.ReadCommandNames(output));

    [Theory]
    [InlineData("rest --method GET", "rest")]
    [InlineData("  vm", "vm")]
    [InlineData("--help", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Reads_the_command_verb(string? command, string? expected) =>
        Assert.Equal(expected, PlanCommandNormalizer.VerbOf(command));
}
