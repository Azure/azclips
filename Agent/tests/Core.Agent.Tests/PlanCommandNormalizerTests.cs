using Core.Agent.Hosting;
using Core.Agent.Plan;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for <see cref="PlanCommandNormalizer"/> — restoring the missing
/// <c>az</c> on plan steps that call an azclips command group, while leaving
/// complete az commands and other programs alone.
/// </summary>
public class PlanCommandNormalizerTests
{
    private static IReadOnlyList<ConsoleApprovalPrompt.PlanStep> Steps(params string[] commands) =>
        commands.Select((command, index) => new ConsoleApprovalPrompt.PlanStep(index + 1, $"step {index + 1}", command)).ToList();

    [Theory]
    [InlineData("rest --method GET --uri https://example", "az rest --method GET --uri https://example")]
    [InlineData("vm create --name test-vm", "az vm create --name test-vm")]
    [InlineData("  deployment group create --name d", "az deployment group create --name d")]
    public void Adds_az_to_built_in_command_groups(string command, string expected)
    {
        var result = PlanCommandNormalizer.EnsureAzPrefix(Steps(command), failedCommandVerb: null);

        Assert.Equal(expected, Assert.Single(result!).Cmd);
    }

    [Theory]
    [InlineData("az vm show --name x")]
    [InlineData("AZ vm show --name x")]
    [InlineData("ssh-keygen -t rsa")]
    [InlineData("New-AzVM -Name x")]
    [InlineData("--help")]
    public void Leaves_az_commands_and_other_programs_unchanged(string command)
    {
        var steps = Steps(command);

        var result = PlanCommandNormalizer.EnsureAzPrefix(steps, failedCommandVerb: "rest");

        Assert.Same(steps, result);
    }

    [Fact]
    public void Uses_the_failed_command_verb_for_extension_command_groups()
    {
        Assert.Equal("az aks list", Assert.Single(PlanCommandNormalizer.EnsureAzPrefix(Steps("aks list"), "aks")!).Cmd);
        Assert.Equal("aks list", Assert.Single(PlanCommandNormalizer.EnsureAzPrefix(Steps("aks list"), "rest")!).Cmd);
    }

    [Fact]
    public void Fixes_only_the_steps_that_need_it()
    {
        var result = PlanCommandNormalizer.EnsureAzPrefix(Steps("az group create -n g -l eastus", "rest --method GET"), "rest");

        Assert.Equal(new[] { "az group create -n g -l eastus", "az rest --method GET" }, result!.Select(step => step.Cmd));
        Assert.Equal(new[] { 1, 2 }, result!.Select(step => step.Id));
    }

    [Fact]
    public void Returns_null_for_no_steps() =>
        Assert.Null(PlanCommandNormalizer.EnsureAzPrefix(null, "rest"));

    [Theory]
    [InlineData("rest --method GET", "rest")]
    [InlineData("  vm", "vm")]
    [InlineData("--help", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Reads_the_command_verb(string? command, string? expected) =>
        Assert.Equal(expected, PlanCommandNormalizer.VerbOf(command));
}
