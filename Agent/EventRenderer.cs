using System.Collections.Concurrent;
using System.Diagnostics;
using GitHub.Copilot;

namespace Core.Agent;

/// <summary>
/// Translates Copilot SDK <see cref="SessionEvent"/>s into <see cref="AgentEvent"/>s
/// for azclips's host-agnostic rendering layer.
///
/// Pattern:    streaming-dedup via per-turn flag + first-token timestamp.
///             Industry research §2: FinishReason is unreliable for dedup;
///             tracking whether we've emitted streaming text this turn lets
///             non-streaming aggregate messages render only when no Delta
///             arrived (e.g. BYOM providers without streaming).
///             Pattern:    ToolCallId → ToolName dictionary so Complete events
///             can render the human-readable name (the SDK schema does not put
///             ToolName on ToolExecutionCompleteData; only Start carries it).
/// Avoid:      ❌ rendering AssistantMessage if we already streamed Delta —
///                produces visible duplication.
/// </summary>
public sealed class EventRenderer
{
    public Action<AgentEvent>? OnEvent { get; set; }
    public Action<string>? OnText { get; set; }

    /// <summary>Returns the model the gateway served, or null when it is not known.</summary>
    internal Func<string?>? ServedModel { get; set; }

    // Keyed by ToolCallId. ToolExecutionCompleteData carries neither ToolName
    // nor an elapsed/duration field, so we side-channel both from the matching
    // Start event. Both entries are TryRemove'd on Complete so a long session
    // doesn't accumulate dead state.
    private readonly ConcurrentDictionary<string, string> _toolNamesByCallId = new();
    private readonly ConcurrentDictionary<string, long> _toolStartTsByCallId = new();
    private long _turnStartTs;
    private bool _streamingEmitted;

    public void Forward(SessionEvent ev)
    {
        switch (ev)
        {
            case AssistantTurnStartEvent:
                _turnStartTs = Stopwatch.GetTimestamp();
                _streamingEmitted = false;
                break;

            case AssistantMessageDeltaEvent delta:
                var deltaText = delta.Data?.DeltaContent;
                if (!string.IsNullOrEmpty(deltaText))
                {
                    if (!_streamingEmitted)
                    {
                        _streamingEmitted = true;
                        var ttft = Stopwatch.GetElapsedTime(_turnStartTs);
                        OnEvent?.Invoke(new LlmFirstToken(ttft, DateTimeOffset.UtcNow));
                    }
                    OnText?.Invoke(deltaText);
                }
                break;

            case AssistantMessageEvent msg:
                // Render aggregate only if no Delta this turn — otherwise duplicate.
                if (!_streamingEmitted)
                {
                    var content = msg.Data?.Content;
                    if (!string.IsNullOrEmpty(content)) OnText?.Invoke(content);
                }
                break;

            case ToolExecutionStartEvent ts:
                var startName = ts.Data?.ToolName ?? "?";
                var startCallId = ts.Data?.ToolCallId ?? string.Empty;
                if (startCallId.Length > 0)
                {
                    _toolNamesByCallId[startCallId] = startName;
                    _toolStartTsByCallId[startCallId] = Stopwatch.GetTimestamp();
                }
                OnEvent?.Invoke(new ToolStarted(
                    Name: startName,
                    ArgsJson: ts.Data?.Arguments?.ToString() ?? string.Empty,
                    CallId: startCallId,
                    ParentCallId: null,
                    At: DateTimeOffset.UtcNow));
                break;

            case ToolExecutionCompleteEvent tc:
                var completeCallId = tc.Data?.ToolCallId ?? string.Empty;
                var elapsed = _toolStartTsByCallId.TryRemove(completeCallId, out var startedAt)
                    ? Stopwatch.GetElapsedTime(startedAt)
                    : TimeSpan.Zero;
                var resultText = tc.Data?.Result?.Content ?? string.Empty;
                var errorMsg = tc.Data?.Error?.Message;
                var isError = !string.IsNullOrEmpty(errorMsg) || tc.Data?.Success == false;
                var toolName = _toolNamesByCallId.TryRemove(completeCallId, out var n) ? n : completeCallId;
                if (isError)
                {
                    OnEvent?.Invoke(new ToolFailed(
                        Name: toolName,
                        Elapsed: elapsed,
                        Error: errorMsg ?? "tool failed",
                        CallId: completeCallId,
                        ParentCallId: null,
                        At: DateTimeOffset.UtcNow));
                }
                else
                {
                    OnEvent?.Invoke(new ToolCompleted(
                        Name: toolName,
                        Elapsed: elapsed,
                        FullResult: resultText,
                        IsError: false,
                        CallId: completeCallId,
                        ParentCallId: null,
                        At: DateTimeOffset.UtcNow));
                }
                break;

            case AssistantUsageEvent usage:
                var turnElapsed = Stopwatch.GetElapsedTime(_turnStartTs);
                OnEvent?.Invoke(new LlmTurnCompleted(
                    Model: ServedModel?.Invoke() ?? usage.Data?.Model ?? "unknown",
                    InputTokens: (int)(usage.Data?.InputTokens ?? 0),
                    OutputTokens: (int)(usage.Data?.OutputTokens ?? 0),
                    CachedTokens: (int)(usage.Data?.CacheReadTokens ?? 0),
                    ReasoningTokens: (int?)usage.Data?.ReasoningTokens,
                    CacheWriteTokens: (int?)usage.Data?.CacheWriteTokens,
                    Elapsed: turnElapsed,
                    At: DateTimeOffset.UtcNow));
                break;

            case SessionErrorEvent err:
                // Without surfacing this the host has no completion signal and
                // waits out TurnTimeout. Forwarding it as a first-class event
                // lets the host render it and abort the wait.
                OnEvent?.Invoke(new SessionErrored(
                    ErrorType: err.Data?.ErrorType ?? "unknown",
                    Message: err.Data?.Message ?? "(no message)",
                    ErrorCode: err.Data?.ErrorCode,
                    StatusCode: err.Data?.StatusCode is { } sc ? (int)sc : null,
                    ProviderCallId: err.Data?.ProviderCallId,
                    At: DateTimeOffset.UtcNow));
                break;
        }
    }
}
