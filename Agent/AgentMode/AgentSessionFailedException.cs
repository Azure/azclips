// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Agent.AgentMode;

/// <summary>
/// Raised when a chat session ends on a terminal SDK failure.
/// </summary>
/// <remarks>
/// <para>The failure has already been shown to the user as <c>✗ session.error [type] message</c> at
/// the point it happened, so handlers must not print it again. This exists purely to carry the
/// terminal state out to <see cref="AgentModeEntry.RunSessionAsync"/>, which otherwise cannot tell
/// a failed session from a successful one: the chat loop returns normally either way.</para>
///
/// <para>Without it a session the user just watched fail exports
/// <c>properties.recovery.outcome=ok</c> with <c>ActivityStatusCode.Ok</c> and exit code 0, so every
/// success-rate metric built on that tag is wrong and callers branching on the exit code cannot
/// detect the failure.</para>
/// </remarks>
internal sealed class AgentSessionFailedException : Exception
{
    public AgentSessionFailedException(string? errorType, string? message)
        : base($"[{errorType ?? "unknown"}] {message}")
    {
        ErrorType = errorType;
    }

    /// <summary>The SDK's error classification, as reported on <c>SessionErrored</c>.</summary>
    public string? ErrorType { get; }
}
