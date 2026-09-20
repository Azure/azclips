using System.Text.Json;
using Core.Agent.Hosting;
using Core.Agent.Plan;
using Core.Agent.Tools.Resource.Cache;
using Microsoft.Extensions.AI;
using Xunit;

namespace Core.Agent.Tests;

public sealed class ProposePlanSchemaTests
{
    [Fact]
    public void SubmitResponse_schema_requires_status_and_advertises_only_legal_values()
    {
        var schema = new SubmitResponseTool().SubmitResponse.JsonSchema;
        var status = schema.GetProperty("properties").GetProperty("status");

        Assert.Equal("string", status.GetProperty("type").GetString());
        Assert.Equal(new[] { "completed", "rejected", "failed" },
            status.GetProperty("enum").EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.Contains("status", schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public void ProposePlan_schema_requires_nonempty_issues_and_steps()
    {
        var schema = new ProposePlanTool().ProposePlan.JsonSchema;
        var properties = schema.GetProperty("properties");
        var required = schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ToArray();

        foreach (var name in new[] { "issues", "steps" })
        {
            var property = properties.GetProperty(name);
            Assert.Equal("array", property.GetProperty("type").GetString());
            Assert.Equal(1, property.GetProperty("minItems").GetInt32());
            Assert.Contains(name, required);
        }
        Assert.DoesNotContain("placeholders", required);
        Assert.DoesNotContain("defaults", required);
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("rejected")]
    [InlineData("failed")]
    public async Task SubmitResponse_accepts_wire_arguments_and_preserves_logged_status(string status)
    {
        var message = $"OUT_OF_SCOPE_schema_test_{Guid.NewGuid():N}";
        var arguments = new { status, message };
        Assert.Null(PlanArgsParser.ValidateSubmitResponse(JsonSerializer.SerializeToElement(arguments)));

        var result = await new SubmitResponseTool().SubmitResponse.InvokeAsync(ToArguments(arguments));

        Assert.Equal("session terminated", JsonSerializer.SerializeToElement(result).GetString());
        Assert.Contains(ToolInvocationLog.Snapshot(),
            entry => entry.Name == "submit_response" &&
                     entry.Args == $"status={status}" &&
                     entry.Result == $"{status}: {message}");
    }

    [Fact]
    public async Task ProposePlan_accepts_valid_wire_arguments_with_optional_fields_omitted()
    {
        var arguments = new
        {
            issues = new[] { "A command needs correction." },
            steps = new[] { new { id = 1, label = "Inspect the result", cmd = "echo ready" } },
        };
        Assert.Null(PlanArgsParser.ValidateProposePlan(JsonSerializer.SerializeToElement(arguments)));

        var result = await new ProposePlanTool().ProposePlan.InvokeAsync(ToArguments(arguments));

        Assert.Equal("plan submitted for user review", JsonSerializer.SerializeToElement(result).GetString());
    }

    private static AIFunctionArguments ToArguments(object arguments) =>
        new(JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(arguments))!);
}
