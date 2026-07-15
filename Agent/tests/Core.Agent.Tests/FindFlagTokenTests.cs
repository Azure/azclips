// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Core.Agent.Hosting;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for <see cref="ConsoleApprovalPrompt.FindFlagToken"/> — the render-
/// time lookup that reads a review flag's real token out of the plan's step
/// command, so "Values to review" always matches what will run (no LLM-supplied
/// value, no "active true" mis-shaping).
/// </summary>
public class FindFlagTokenTests
{
    private static IReadOnlyList<ConsoleApprovalPrompt.PlanStep> Steps(params string[] cmds)
    {
        var list = new List<ConsoleApprovalPrompt.PlanStep>();
        for (var i = 0; i < cmds.Length; i++)
            list.Add(new ConsoleApprovalPrompt.PlanStep(i + 1, "step", cmds[i]));
        return list;
    }

    [Fact]
    public void ValuedFlag_ReturnsFlagWithValue()
    {
        var steps = Steps("az vm create --name myvm --location eastus2 --size Standard_D2s_v5");
        Assert.Equal("--location eastus2", ConsoleApprovalPrompt.FindFlagToken(steps, "--location"));
    }

    [Fact]
    public void SwitchFlag_ReturnsBareFlag_NoValue()
    {
        // The whole point: `--active` is a switch, so it renders as "--active",
        // never "--active true".
        var steps = Steps("az profile show --active");
        Assert.Equal("--active", ConsoleApprovalPrompt.FindFlagToken(steps, "--active"));
    }

    [Fact]
    public void SwitchFollowedByAnotherFlag_DoesNotSwallowIt()
    {
        var steps = Steps("az vm delete --force --name myvm");
        Assert.Equal("--force", ConsoleApprovalPrompt.FindFlagToken(steps, "--force"));
    }

    [Fact]
    public void PlaceholderValue_IsReturnedAsWritten()
    {
        var steps = Steps("az vm create --name myvm --admin-password <admin-password>");
        Assert.Equal("--admin-password <admin-password>", ConsoleApprovalPrompt.FindFlagToken(steps, "--admin-password"));
    }

    [Fact]
    public void EqualsForm_IsMatched()
    {
        var steps = Steps("az vm create --location=eastus2");
        Assert.Equal("--location eastus2", ConsoleApprovalPrompt.FindFlagToken(steps, "--location"));
    }

    [Fact]
    public void FlagGivenWithoutDashes_IsNormalized()
    {
        // The model may list the flag as "location"; we still find "--location".
        var steps = Steps("az vm create --location eastus2");
        Assert.Equal("--location eastus2", ConsoleApprovalPrompt.FindFlagToken(steps, "location"));
    }

    [Fact]
    public void NotInAnyStep_FallsBackToFlagAlone()
    {
        var steps = Steps("az group create --name my-rg");
        Assert.Equal("--nonexistent", ConsoleApprovalPrompt.FindFlagToken(steps, "--nonexistent"));
    }

    [Fact]
    public void FoundInSecondStep()
    {
        var steps = Steps(
            "az group create --name my-rg --location eastus2",
            "az vm create --name myvm --size Standard_D2s_v5");
        Assert.Equal("--size Standard_D2s_v5", ConsoleApprovalPrompt.FindFlagToken(steps, "--size"));
    }

    [Fact]
    public void PrefixFlag_DoesNotFalseMatchLongerFlag()
    {
        // "--resource-group" must not be matched when looking for "--resource".
        var steps = Steps("az vm show --resource-group my-rg");
        Assert.Equal("--resource", ConsoleApprovalPrompt.FindFlagToken(steps, "--resource"));
    }
}
