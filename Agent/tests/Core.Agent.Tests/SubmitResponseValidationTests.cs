using System.Text.Json;
using Core.Agent;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for <see cref="ChatAgent.ValidateSubmitResponse"/> — the PP-stage
/// argument validation that runs in OnPreToolUse and, on failure, denies the
/// submit_response call with a correction prompt instead of throwing.
///
/// These assert the real validation rules (status enum, message-required,
/// OUT_OF_SCOPE_ prefix) against SDK-shaped JsonElement args. The downstream
/// deny + additionalContext round-trip itself is an e2e concern, not covered here.
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

        var invalid = ChatAgent.ValidateSubmitResponse(Args(payload), out var error);

        Assert.False(invalid);
        Assert.Empty(error);
    }

    [Fact]
    public void Rejects_missing_status()
    {
        var invalid = ChatAgent.ValidateSubmitResponse(Args(new { message = "hi" }), out var error);

        Assert.True(invalid);
        Assert.Contains("status", error);
    }

    [Fact]
    public void Rejects_unknown_status()
    {
        var invalid = ChatAgent.ValidateSubmitResponse(Args(new { status = "done" }), out var error);

        Assert.True(invalid);
        Assert.Contains("done", error);
    }

    [Theory]
    [InlineData("rejected")]
    [InlineData("failed")]
    public void Rejects_missing_message_when_required(string status)
    {
        var invalid = ChatAgent.ValidateSubmitResponse(Args(new { status }), out var error);

        Assert.True(invalid);
        Assert.Contains("message", error);
    }

    [Fact]
    public void Rejects_rejected_message_without_scope_prefix()
    {
        var invalid = ChatAgent.ValidateSubmitResponse(
            Args(new { status = "rejected", message = "not relevant" }), out var error);

        Assert.True(invalid);
        Assert.Contains("OUT_OF_SCOPE_", error);
    }

    [Fact]
    public void Completed_does_not_require_message()
    {
        var invalid = ChatAgent.ValidateSubmitResponse(Args(new { status = "completed" }), out _);

        Assert.False(invalid);
    }
}
