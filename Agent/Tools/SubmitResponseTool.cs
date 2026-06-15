using System.ComponentModel;
using Core.Agent.Tools.Cache;
using Microsoft.Extensions.AI;

namespace Core.Agent.Tools;

/// <summary>
/// Terminal tool every agent session ends with. The LLM calls submit_response
/// to signal the session reached a final state instead of trailing off into
/// free-form text the host can't classify:
///   • completed — the user's task is done (message optional, becomes a summary)
///   • rejected  — request is out of scope (message = OUT_OF_SCOPE_&lt;code&gt;)
///   • failed    — tried but couldn't finish (message = human-readable reason)
///
/// The host recognizes the call in OnPreToolUse and renders the final outcome.
/// Argument validation (status enum, message-required rules) lives in the hook,
/// not this method body, so the LLM gets a deny + correction prompt rather than
/// a framework stack trace. This body only logs the call.
/// </summary>
public sealed class SubmitResponseTool
{
    public SubmitResponseTool()
    {
        SubmitResponse = AIFunctionFactory.Create(SubmitResponseImpl, new AIFunctionFactoryOptions
        {
            Name = "submit_response",
            Description =
                "Terminate the agent session and return the final outcome to the host. " +
                "MUST be the last tool you call in every session. " +
                "Use 'completed' when the user's task is done (message optional, becomes the summary). " +
                "Use 'rejected' when the request is out of scope (message = OUT_OF_SCOPE_<code>; see the scope rules in your system prompt). " +
                "Use 'failed' when you tried but couldn't complete (message = a short human-readable reason).",
        });

        All = new[] { SubmitResponse };
    }

    public AIFunction SubmitResponse { get; }
    public IReadOnlyList<AIFunction> All { get; }

    [Description("Terminate the session with a final outcome.")]
    private static string SubmitResponseImpl(
        [Description("Session outcome: 'completed' | 'rejected' | 'failed'.")]
        string status,
        [Description("For 'completed': optional summary. For 'rejected'/'failed': required reason. Rejected messages must start with OUT_OF_SCOPE_.")]
        string? message = null)
    {
        using var scope = ToolInvocationLog.Start("submit_response", $"status={status}");
        scope.SetResult(string.IsNullOrEmpty(message) ? status : $"{status}: {message}");
        return "session terminated";
    }
}
