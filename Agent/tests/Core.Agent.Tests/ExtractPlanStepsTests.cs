using System.Text.Json;
using Core.Agent;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for <see cref="ChatAgent.ExtractPlanSteps"/> — parsing the
/// validated steps array into the host's PlanStep render view. Covers the real
/// branches: normal parse, id fallback when absent, destructive flagging
/// (model flag + host danger-verb backstop), and empty/no steps → null.
/// </summary>
public class ExtractPlanStepsTests
{
    private static JsonElement Args(object obj) => JsonSerializer.SerializeToElement(obj);

    [Fact]
    public void Parses_steps_with_ids()
    {
        var args = Args(new
        {
            planMd = "plan",
            steps = new[]
            {
                new { id = 1, label = "Create RG", cmd = "az group create -n foo" },
                new { id = 2, label = "Retry",     cmd = "az vm create -n bar" },
            },
        });

        var steps = ChatAgent.ExtractPlanSteps(args);

        Assert.NotNull(steps);
        Assert.Equal(2, steps!.Count);
        Assert.Equal(1, steps[0].Id);
        Assert.Equal("Create RG", steps[0].Label);
        Assert.Equal("az group create -n foo", steps[0].Cmd);
        Assert.Equal(2, steps[1].Id);
    }

    [Fact]
    public void Falls_back_to_positional_id_when_absent()
    {
        var args = Args(new
        {
            planMd = "plan",
            steps = new[]
            {
                new { label = "First", cmd = "az a" },
                new { label = "Second", cmd = "az b" },
            },
        });

        var steps = ChatAgent.ExtractPlanSteps(args);

        Assert.NotNull(steps);
        Assert.Equal(1, steps![0].Id);
        Assert.Equal(2, steps[1].Id);
    }

    [Fact]
    public void Falls_back_to_positional_id_when_id_is_a_string()
    {
        // Models often emit "id":"1" (string) instead of a number. The parse
        // must not throw on a non-number id — it falls back to positional id.
        var args = Args(new
        {
            planMd = "plan",
            steps = new[]
            {
                new { id = "1", label = "First", cmd = "az a" },
                new { id = "two", label = "Second", cmd = "az b" },
            },
        });

        var steps = ChatAgent.ExtractPlanSteps(args);

        Assert.NotNull(steps);
        Assert.Equal(2, steps!.Count);
        Assert.Equal(1, steps[0].Id);
        Assert.Equal(2, steps[1].Id);
    }

    [Fact]
    public void Marks_step_destructive_when_model_sets_the_flag()
    {
        var args = Args(new
        {
            planMd = "plan",
            steps = new[]
            {
                new { id = 1, label = "Tag it", cmd = "az group update -n foo", destructive = true },
            },
        });

        var steps = ChatAgent.ExtractPlanSteps(args);

        Assert.NotNull(steps);
        Assert.True(steps![0].Destructive);
    }

    [Fact]
    public void Marks_step_destructive_via_danger_verb_when_model_omits_it()
    {
        // The model didn't flag it, but the command deletes — the host backstop
        // must still mark it destructive so the user sees the red warning.
        var args = Args(new
        {
            planMd = "plan",
            steps = new[]
            {
                new { id = 1, label = "Remove RG", cmd = "az group delete -n foo --yes" },
            },
        });

        var steps = ChatAgent.ExtractPlanSteps(args);

        Assert.NotNull(steps);
        Assert.True(steps![0].Destructive);
    }

    [Fact]
    public void Leaves_safe_step_not_destructive()
    {
        var args = Args(new
        {
            planMd = "plan",
            steps = new[]
            {
                new { id = 1, label = "List groups", cmd = "az group list" },
            },
        });

        var steps = ChatAgent.ExtractPlanSteps(args);

        Assert.NotNull(steps);
        Assert.False(steps![0].Destructive);
    }

    [Fact]
    public void Destructive_only_trusts_the_schema_boolean_not_string_or_number()
    {
        // destructive is a schema boolean — we no longer accept the string "true"
        // or a number 1 (that host-side fallback was removed; the schema is the
        // contract). A safe command with a non-boolean destructive value stays
        // not-destructive; the danger-verb backstop is the only other path.
        var stringArgs = Args(new
        {
            planMd = "plan",
            steps = new[] { new { id = 1, label = "Tag it", cmd = "az group update -n foo", destructive = "true" } },
        });
        var numberArgs = Args(new
        {
            planMd = "plan",
            steps = new[] { new { id = 1, label = "Tag it", cmd = "az group update -n foo", destructive = 1 } },
        });

        Assert.False(ChatAgent.ExtractPlanSteps(stringArgs)![0].Destructive);
        Assert.False(ChatAgent.ExtractPlanSteps(numberArgs)![0].Destructive);
    }

    [Fact]
    public void Returns_null_when_steps_absent()
    {
        var steps = ChatAgent.ExtractPlanSteps(Args(new { planMd = "plan" }));
        Assert.Null(steps);
    }

    [Fact]
    public void Returns_null_when_steps_empty()
    {
        var steps = ChatAgent.ExtractPlanSteps(Args(new { planMd = "plan", steps = Array.Empty<object>() }));
        Assert.Null(steps);
    }
}
