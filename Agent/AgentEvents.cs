namespace Core.Agent;

// Pattern:    Host-agnostic event surface for HITL / tool lifecycle.
//             Today's producer is ChatAgent (Copilot SDK); the projection
//             keeps Classic CLI, future TUI, and telemetry rendering uniform.
// Avoid:      ❌ trimming / truncating data at the event source — full data
//                stays in the event so TUI consumers can show "expand to
//                see full args / result", classic CLI trims at render time.
// Avoid:      ❌ reusing the same event type for parent and nested calls
//                without a parent linkage — CallId / ParentCallId let
//                consumers build the call tree without timing heuristics.

/// <summary>
/// Streaming events emitted by the agent so hosts can render per-tool /
/// per-LLM activity (▶ start, ✓ end, ✗ error). Tool events carry CallId so
/// nested calls can be linked via ParentCallId.
/// </summary>
public abstract record AgentEvent(DateTimeOffset At);

// ── LLM events (path-agnostic) ────────────────────────────────────────

/// <summary>First streaming token of a turn arrived — useful for TTFT measurement.</summary>
public sealed record LlmFirstToken(TimeSpan TimeToFirstToken, DateTimeOffset At) : AgentEvent(At);

/// <summary>One LLM round-trip just completed (text + tool_calls finalized).</summary>
public sealed record LlmTurnCompleted(int InputTokens, int OutputTokens, int CachedTokens, TimeSpan Elapsed, DateTimeOffset At)
    : AgentEvent(At);

/// <summary>
/// The model endpoint rejected a request (for example, 400
/// <c>invalid_encrypted_content</c> when a load balancer sends a conversation to
/// another region), and the request was sent again once without the encrypted
/// reasoning items it could not verify.
/// </summary>
public sealed record LlmRequestRecovered(string Reason, int StatusCode, int RemovedReasoningItems, DateTimeOffset At)
    : AgentEvent(At);

// ── Tool events (path-agnostic) ───────────────────────────────────────

/// <summary>
/// Local tool invocation about to start.
/// CallId uniquely identifies this call; ParentCallId links to the outer tool
/// when this call happens inside a sub-agent (e.g. find_resources fired by
/// the diagnose agent that was itself invoked via invoke_diagnose).
/// </summary>
public sealed record ToolStarted(
    string Name,
    string ArgsJson,
    string CallId,
    string? ParentCallId,
    DateTimeOffset At) : AgentEvent(At);

/// <summary>
/// Local tool invocation finished successfully.
/// <see cref="FullResult"/> is NOT trimmed — render layer decides display length.
/// <see cref="IsError"/> = true when the tool itself returned an error result
/// (e.g. <c>bash</c> exiting non-zero); distinct from
/// <see cref="ToolFailed"/> which is for tool implementation exceptions.
/// </summary>
public sealed record ToolCompleted(
    string Name,
    TimeSpan Elapsed,
    string FullResult,
    bool IsError,
    string CallId,
    string? ParentCallId,
    DateTimeOffset At) : AgentEvent(At);

/// <summary>Local tool invocation threw.</summary>
public sealed record ToolFailed(
    string Name,
    TimeSpan Elapsed,
    string Error,
    string CallId,
    string? ParentCallId,
    DateTimeOffset At) : AgentEvent(At);

// ── Sub-agent lifecycle (chat → invoke_diagnose) ──────────────────────
//
// When the chat agent calls a sub-agent (via AIAgent.AsAIFunction), we emit
// these higher-level boundary events instead of plain Tool events so renderers
// can clearly mark the sub-agent's lifetime as a separate "scope" containing
// its own nested ToolStarted / ToolCompleted (linked by ParentCallId).

/// <summary>Chat agent invoked a sub-agent via its AsAIFunction wrapper.</summary>
public sealed record SubAgentInvokeStarted(
    string AgentName,
    string Query,
    string CallId,
    string? ParentCallId,
    DateTimeOffset At) : AgentEvent(At);

/// <summary>Sub-agent invocation finished successfully.</summary>
public sealed record SubAgentInvokeCompleted(
    string AgentName,
    TimeSpan Elapsed,
    string Response,
    string CallId,
    string? ParentCallId,
    DateTimeOffset At) : AgentEvent(At);

/// <summary>Sub-agent invocation threw.</summary>
public sealed record SubAgentInvokeFailed(
    string AgentName,
    TimeSpan Elapsed,
    string Error,
    string CallId,
    string? ParentCallId,
    DateTimeOffset At) : AgentEvent(At);

// ── Session / upstream errors ─────────────────────────────────────────
//
// session.error is the SDK's authoritative "something upstream broke" signal —
// 401 / quota / rate-limit / context-limit / etc. Without listening for this
// the host sees no completion event and silently hangs on the turn timeout.

/// <summary>
/// Upstream / SDK-level error. Distinct from <see cref="ToolFailed"/> (which
/// is a single tool throwing) — this means the entire session can't proceed
/// without intervention (re-auth, switch model, wait out rate-limit, etc.).
/// Hosts should render this prominently and stop waiting on the current turn.
/// </summary>
public sealed record SessionErrored(
    string ErrorType,
    string Message,
    string? ErrorCode,
    int? StatusCode,
    string? ProviderCallId,
    DateTimeOffset At) : AgentEvent(At);

// ── Host-side log routing ─────────────────────────────────────────────
//
// Azclips's own services (MsalAuthService / AuthContextService / Engine) call
// ILoggingService directly and historically wrote to stdout, racing the
// spinner. In agent mode we redirect that channel to the renderer via this
// event so every byte the user sees goes through the renderer's lock.
// Verbose / Debug / Info / Log are dropped (noise); Warn / Error are
// promoted to events so nothing is silently lost.

public enum HostLogLevel { Warn, Error }

/// <summary>
/// A Warn / Error from azclips's own services (not the LLM, not a tool).
/// Emitted by RendererLoggingService so all stdout writes funnel through
/// IAgentRenderer instead of bypassing its lock.
/// </summary>
public sealed record HostLogEmitted(
    HostLogLevel Level,
    string Message,
    DateTimeOffset At) : AgentEvent(At);

// ── Sink delegate ─────────────────────────────────────────────────────

/// <summary>Synchronous sink callback. If a sink wants I/O it should buffer or fire-and-forget.</summary>
public delegate void AgentEventSink(AgentEvent ev);
