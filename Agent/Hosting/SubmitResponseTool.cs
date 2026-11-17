using System.ComponentModel;
using Core.Agent.Tools.Resource.Cache;
using GitHub.Copilot;
using Microsoft.Extensions.AI;

namespace Core.Agent.Hosting;

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
    private readonly Func<string, IDisposable?>? _activityCreator;

    // endsTurn: a successful call ends the agent turn (one-shot), so the model does
    // not answer once more after the session outcome is shown.
    public SubmitResponseTool(bool endsTurn = false, Func<string, IDisposable?>? activityCreator = null)
    {
        _activityCreator = activityCreator;
        var options = new AIFunctionFactoryOptions
        {
            Name = "submit_response",
            Description =
                "End this turn by returning its outcome and showing it to the user. Always call this once, as the last tool of the turn. " +
                "Use 'completed' when the task is done (the plan is the summary; add a message only for critical info not already in the plan). " +
                "Use 'rejected' when the request is out of scope (message = OUT_OF_SCOPE_<code>; see the scope rules in your system prompt). " +
                "Use 'failed' when you tried but couldn't complete (message = a short human-readable reason).",
        };
        SubmitResponse = endsTurn
            ? CopilotTool.DefineTool(SubmitResponseImpl, new CopilotToolOptions { IsTerminal = true }, options)
            : AIFunctionFactory.Create(SubmitResponseImpl, options);

        All = new[] { SubmitResponse };
    }

    public AIFunction SubmitResponse { get; }
    public IReadOnlyList<AIFunction> All { get; }

    [Description("Terminate the session with a final outcome.")]
    private string SubmitResponseImpl(
        [Description("Session outcome: 'completed' | 'rejected' | 'failed'.")]
        string status,
        [Description("For 'completed': only add a message if it carries critical information NOT already in the plan or needsUserInput — otherwise leave it empty (don't repeat the plan). For 'rejected': required, must start with OUT_OF_SCOPE_. For 'failed': required, a short human-readable reason.")]
        string? message = null)
    {
        using var scope = ToolInvocationLog.Start("submit_response", $"status={status}", _activityCreator);
        scope.SetResult(string.IsNullOrEmpty(message) ? status : $"{status}: {message}");
        // The status is a closed set; the free-text message is never recorded.
        scope.RecordRequest(status is "completed" or "rejected" or "failed" ? $"status={status}" : "status=invalid");
        scope.RecordResponse("ok");
        return "session terminated";
    }
}
