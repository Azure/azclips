using System.Text.Json;
using Core.Agent;
using Core.Agent.Plan;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for <see cref="PlanArgsParser.ValidateSubmitResponse"/> — the PP-stage
/// argument validation that runs in OnPreToolUse and, on failure, denies the
/// submit_response call with a correction prompt instead of throwing.
///
/// These assert the real validation rules (status enum, message-required,
/// OUT_OF_SCOPE_ prefix) against SDK-shaped JsonElement args; returns null when
/// valid, else a message. The deny + additionalContext round-trip is an e2e concern.
/// </summary>
public class SubmitResponseValidationTests
{
    // Mirrors how the SDK hands ToolArgs to the hook: a JSON object element.
    private static JsonElement Args(object obj) => JsonSerializer.SerializeToElement(obj);

    [Theory]
    [InlineData("completed")]
    [InlineData("rejected")]
    [InlineData("failed")]
    public void Accepts_valid_status_with_appropriate_message(string status)
    {
        // completed needs no message; rejected/failed need one (rejected prefixed).
        object payload = status switch
        {
            "rejected" => new { status, message = "OUT_OF_SCOPE_other" },
            "failed"   => new { status, message = "ARM returned 403" },
            _          => new { status },
        };

        var error = PlanArgsParser.ValidateSubmitResponse(Args(payload));

        Assert.Null(error);
    }

    [Fact]
    public void Rejects_missing_status()
    {
        var error = PlanArgsParser.ValidateSubmitResponse(Args(new { message = "hi" }));

        Assert.NotNull(error);
        Assert.Contains("status", error);
    }

    [Fact]
    public void Rejects_unknown_status()
    {
        var error = PlanArgsParser.ValidateSubmitResponse(Args(new { status = "done" }));

        Assert.NotNull(error);
        Assert.Contains("done", error);
    }

    [Theory]
    [InlineData("rejected")]
    [InlineData("failed")]
    public void Rejects_missing_message_when_required(string status)
    {
        var error = PlanArgsParser.ValidateSubmitResponse(Args(new { status }));

        Assert.NotNull(error);
        Assert.Contains("message", error);
    }

    [Fact]
    public void Rejects_rejected_message_without_scope_prefix()
    {
        var error = PlanArgsParser.ValidateSubmitResponse(
            Args(new { status = "rejected", message = "not relevant" }));

        Assert.NotNull(error);
        Assert.Contains("OUT_OF_SCOPE_", error);
    }

    [Fact]
    public void Completed_does_not_require_message()
    {
        var error = PlanArgsParser.ValidateSubmitResponse(Args(new { status = "completed" }));

        Assert.Null(error);
    }
}
