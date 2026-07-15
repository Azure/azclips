// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Core.Agent.Hosting;
using Core.Agent.Plan;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for <see cref="PlanSanitizer"/> — deterministic plan
/// post-processing run before rendering. The one rule ("profile-show-active-switch")
/// is PRECISE: it only touches "active true" when the plan actually contains an
/// `az profile show` command. These tests pin that precision — most importantly
/// that a same-named flag on a different command, and valued booleans like
/// `config set --value true`, are never touched.
/// </summary>
public class PlanSanitizerTests
{
    private static ConsoleApprovalPrompt.PlanContent Plan(
        IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? steps = null,
        IReadOnlyList<ConsoleApprovalPrompt.ChosenDefault>? defaults = null) =>
        new(Issues: null, Steps: steps, Placeholders: null, Defaults: defaults);

    private static ConsoleApprovalPrompt.PlanStep Step(string cmd) => new(1, "step", cmd);
    private static ConsoleApprovalPrompt.ChosenDefault Def(string name, string value) => new(name, value, "note");

    // --- The target case: profile show + "active true" in the review block ----

    [Fact]
    public void ProfileShow_ActiveTrue_Default_IsBlanked()
    {
        var plan = Plan(
            steps: new[] { Step("az profile show --active") },
            defaults: new[] { Def("active", "true") });

        var result = PlanSanitizer.Sanitize(plan);

        Assert.Equal(string.Empty, result.Defaults![0].Value); // "active true" -> "active"
        Assert.Equal("active", result.Defaults[0].Name);
        Assert.Equal("note", result.Defaults[0].Note);         // note preserved
    }

    [Fact]
    public void ProfileShow_ActiveTrue_StepCmd_IsCollapsed()
    {
        var plan = Plan(steps: new[] { Step("az profile show --active true") });

        var result = PlanSanitizer.Sanitize(plan);

        Assert.Equal("az profile show --active", result.Steps![0].Cmd);
    }

    // --- Precision: the SAME "active true" must NOT be touched elsewhere -------

    [Fact]
    public void ActiveTrue_WithoutProfileShow_IsUntouched()
    {
        // Some other command lists an "active"/"true" default. Without a
        // profile-show command in the plan, the rule must not fire.
        var plan = Plan(
            steps: new[] { Step("az some other command --active true") },
            defaults: new[] { Def("active", "true") });

        var result = PlanSanitizer.Sanitize(plan);

        Assert.Equal("true", result.Defaults![0].Value);                 // untouched
        Assert.Equal("az some other command --active true", result.Steps![0].Cmd); // untouched
    }

    // --- Reservation: valued booleans must NEVER be touched -------------------

    [Fact]
    public void ConfigSet_ValueTrue_IsUntouched_EvenWithProfileShowPresent()
    {
        // Even if a profile-show step is present, the rule only targets the
        // "active" flag — `config set --value true` is left exactly alone.
        var plan = Plan(
            steps: new[]
            {
                Step("az profile show --active"),
                Step("az config set --key agent.enabled --value true"),
            });

        var result = PlanSanitizer.Sanitize(plan);

        Assert.Equal("az config set --key agent.enabled --value true", result.Steps![1].Cmd);
    }

    [Fact]
    public void ProfileShow_ActiveFalse_IsUntouched()
    {
        // Only "true" is dropped; "false" is always meaningful.
        var plan = Plan(
            steps: new[] { Step("az profile show --active") },
            defaults: new[] { Def("active", "false") });

        var result = PlanSanitizer.Sanitize(plan);

        Assert.Equal("false", result.Defaults![0].Value);
    }

    // --- No-ops ---------------------------------------------------------------

    [Fact]
    public void EmptyPlan_DoesNotThrow()
    {
        var result = PlanSanitizer.Sanitize(Plan());
        Assert.Null(result.Steps);
        Assert.Null(result.Defaults);
    }

    [Fact]
    public void ProfileShow_NoActiveDefault_IsNoOp()
    {
        var plan = Plan(
            steps: new[] { Step("az profile show --name myprofile") },
            defaults: new[] { Def("name", "myprofile") });

        var result = PlanSanitizer.Sanitize(plan);

        Assert.Equal("myprofile", result.Defaults![0].Value);
        Assert.Equal("az profile show --name myprofile", result.Steps![0].Cmd);
    }
}
