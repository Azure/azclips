// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Agent.AgentMode;

/// <summary>Carries a terminal SDK failure to the session outcome handler.</summary>
/// <remarks>
/// The renderer already showed the error; handlers must not print it again.
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
