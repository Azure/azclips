using System.Text.Json;
using Core.Agent;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for <see cref="ChatAgent.ExtractPlanSteps"/> — parsing the
/// validated steps array into the host's PlanStep render view. Covers the real
/// branches: normal parse, id fallback when absent, and empty/no steps → null.
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
                new { id = 1, label = "Create RG", cmd = "azv2 group create -n foo" },
                new { id = 2, label = "Retry",     cmd = "azv2 vm create -n bar" },
            },
        });

        var steps = ChatAgent.ExtractPlanSteps(args);

        Assert.NotNull(steps);
        Assert.Equal(2, steps!.Count);
        Assert.Equal(1, steps[0].Id);
        Assert.Equal("Create RG", steps[0].Label);
        Assert.Equal("azv2 group create -n foo", steps[0].Cmd);
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
                new { label = "First", cmd = "azv2 a" },
                new { label = "Second", cmd = "azv2 b" },
            },
        });

        var steps = ChatAgent.ExtractPlanSteps(args);

        Assert.NotNull(steps);
        Assert.Equal(1, steps![0].Id);
        Assert.Equal(2, steps[1].Id);
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
