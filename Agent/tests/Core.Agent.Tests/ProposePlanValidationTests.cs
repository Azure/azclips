using System.Text.Json;
using Core.Agent;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for <see cref="ChatAgent.ValidateProposePlan"/> — the PP-stage
/// validation run in OnPreToolUse before a plan is rendered for approval. Asserts
/// the real rules (planMd non-empty, at least one step, each step has label+cmd)
/// against SDK-shaped JsonElement args. The render + approval round-trip is e2e.
/// </summary>
public class ProposePlanValidationTests
{
    private static JsonElement Args(object obj) => JsonSerializer.SerializeToElement(obj);

    private static object Step(int id, string label, string cmd) =>
        new { id, label, cmd };

    [Fact]
    public void Accepts_valid_plan()
    {
        var args = Args(new
        {
            planMd = "I'll create the resource group, then retry.",
            steps = new[] { Step(1, "Create RG", "azv2 group create -n foo -l eastus") },
        });

        var invalid = ChatAgent.ValidateProposePlan(args, out var error);

        Assert.False(invalid);
        Assert.Empty(error);
    }

    [Fact]
    public void Rejects_empty_planMd()
    {
        var args = Args(new
        {
            planMd = "",
            steps = new[] { Step(1, "Create RG", "azv2 group create -n foo") },
        });

        var invalid = ChatAgent.ValidateProposePlan(args, out var error);

        Assert.True(invalid);
        Assert.Contains("planMd", error);
    }

    [Fact]
    public void Rejects_empty_steps()
    {
        var args = Args(new { planMd = "do a thing", steps = Array.Empty<object>() });

        var invalid = ChatAgent.ValidateProposePlan(args, out var error);

        Assert.True(invalid);
        Assert.Contains("steps", error);
    }

    [Fact]
    public void Rejects_missing_steps_field()
    {
        var args = Args(new { planMd = "do a thing" });

        var invalid = ChatAgent.ValidateProposePlan(args, out var error);

        Assert.True(invalid);
        Assert.Contains("steps", error);
    }

    [Fact]
    public void Rejects_step_missing_label()
    {
        var args = Args(new
        {
            planMd = "plan",
            steps = new[] { new { id = 1, cmd = "azv2 group list" } },
        });

        var invalid = ChatAgent.ValidateProposePlan(args, out var error);

        Assert.True(invalid);
        Assert.Contains("label", error);
    }

    [Fact]
    public void Rejects_step_missing_cmd()
    {
        var args = Args(new
        {
            planMd = "plan",
            steps = new[] { new { id = 1, label = "List groups" } },
        });

        var invalid = ChatAgent.ValidateProposePlan(args, out var error);

        Assert.True(invalid);
        Assert.Contains("cmd", error);
    }

    [Fact]
    public void Reports_index_of_bad_step()
    {
        var args = Args(new
        {
            planMd = "plan",
            steps = new object[]
            {
                Step(1, "Good", "azv2 group list"),
                new { id = 2, label = "No cmd here" },
            },
        });

        var invalid = ChatAgent.ValidateProposePlan(args, out var error);

        Assert.True(invalid);
        Assert.Contains("step 1", error);
        Assert.Contains("cmd", error);
    }
}
