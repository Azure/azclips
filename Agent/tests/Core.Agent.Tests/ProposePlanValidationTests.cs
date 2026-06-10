using System.Text.Json;
using Core.Agent;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for <see cref="ChatAgent.ValidateProposePlan"/> — the PP-stage
/// validation run in OnPreToolUse before a plan is rendered for approval. Asserts
/// the real rules (at least one issue, at least one step, each step has label+cmd)
/// against SDK-shaped JsonElement args; returns null when valid, else a message.
/// The render + approval round-trip is e2e.
/// </summary>
public class ProposePlanValidationTests
{
    private static JsonElement Args(object obj) => JsonSerializer.SerializeToElement(obj);

    private static object Step(int id, string label, string cmd) =>
        new { id, label, cmd };

    private static readonly string[] OneIssue = ["The -g short flag isn't supported."];

    [Fact]
    public void Accepts_valid_plan()
    {
        var args = Args(new
        {
            issues = OneIssue,
            steps = new[] { Step(1, "Create RG", "az group create -n foo -l eastus") },
        });

        var error = ChatAgent.ValidateProposePlan(args);

        Assert.Null(error);
    }

    [Fact]
    public void Rejects_empty_issues()
    {
        var args = Args(new
        {
            issues = Array.Empty<string>(),
            steps = new[] { Step(1, "Create RG", "az group create -n foo") },
        });

        var error = ChatAgent.ValidateProposePlan(args);

        Assert.NotNull(error);
        Assert.Contains("issues", error);
    }

    [Fact]
    public void Rejects_missing_issues_field()
    {
        var args = Args(new { steps = new[] { Step(1, "Create RG", "az group create -n foo") } });

        var error = ChatAgent.ValidateProposePlan(args);

        Assert.NotNull(error);
        Assert.Contains("issues", error);
    }

    [Fact]
    public void Rejects_blank_issue_entry()
    {
        var args = Args(new
        {
            issues = new[] { "   " },
            steps = new[] { Step(1, "Create RG", "az group create -n foo") },
        });

        var error = ChatAgent.ValidateProposePlan(args);

        Assert.NotNull(error);
        Assert.Contains("issue", error);
    }

    [Fact]
    public void Rejects_empty_steps()
    {
        var args = Args(new { issues = OneIssue, steps = Array.Empty<object>() });

        var error = ChatAgent.ValidateProposePlan(args);

        Assert.NotNull(error);
        Assert.Contains("steps", error);
    }

    [Fact]
    public void Rejects_missing_steps_field()
    {
        var args = Args(new { issues = OneIssue });

        var error = ChatAgent.ValidateProposePlan(args);

        Assert.NotNull(error);
        Assert.Contains("steps", error);
    }

    [Fact]
    public void Rejects_step_missing_label()
    {
        var args = Args(new
        {
            issues = OneIssue,
            steps = new[] { new { id = 1, cmd = "az group list" } },
        });

        var error = ChatAgent.ValidateProposePlan(args);

        Assert.NotNull(error);
        Assert.Contains("label", error);
    }

    [Fact]
    public void Rejects_step_missing_cmd()
    {
        var args = Args(new
        {
            issues = OneIssue,
            steps = new[] { new { id = 1, label = "List groups" } },
        });

        var error = ChatAgent.ValidateProposePlan(args);

        Assert.NotNull(error);
        Assert.Contains("cmd", error);
    }

    [Fact]
    public void Reports_index_of_bad_step()
    {
        var args = Args(new
        {
            issues = OneIssue,
            steps = new object[]
            {
                Step(1, "Good", "az group list"),
                new { id = 2, label = "No cmd here" },
            },
        });

        var error = ChatAgent.ValidateProposePlan(args);

        Assert.NotNull(error);
        Assert.Contains("step 1", error);
        Assert.Contains("cmd", error);
    }
}
