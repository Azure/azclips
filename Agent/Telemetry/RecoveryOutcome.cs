// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Core.Agent.Telemetry;

/// <summary>Closed values for <c>properties.recovery.failureReason</c>.</summary>
/// <remarks>
/// Use these values instead of raw runtime or SDK error strings.
/// Unlisted SDK errors map to <see cref="SdkOther"/>.
/// </remarks>
internal enum RecoveryFailure
{
    /// <summary>The SDK reported a terminal failure carrying no error type.</summary>
    Unknown,

    /// <summary>
    /// SDK error type observed in this repository, from the SDK port's own fixture
    /// (<c>AgentTelemetryTests</c>) — which carries a full service payload, not a hand-written one.
    /// </summary>
    RateLimit,

    /// <summary>SDK error type observed in this repository (<c>AgentEventStyleSnapshotTests</c>).</summary>
    Authentication,

    /// <summary>
    /// The SDK reported an error type that is not on the allowlist. Expected to be common —
    /// it measures a vocabulary this repository does not control, and is not a rare-error bucket.
    /// </summary>
    SdkOther,

    /// <summary>The failure was ours rather than the model provider's.</summary>
    HostFault,
}

internal static class RecoveryFailureExtensions
{
    /// <summary>
    /// Exported form of a failure reason. Exhaustive by construction: a member added without a
    /// case here throws on first use instead of exporting a default.
    /// </summary>
    internal static string Wire(this RecoveryFailure failure) => failure switch
    {
        RecoveryFailure.Unknown        => "unknown",
        RecoveryFailure.RateLimit      => "rate_limit",
        RecoveryFailure.Authentication => "authentication",
        RecoveryFailure.SdkOther       => "sdk_other",
        RecoveryFailure.HostFault      => "host_fault",
        _ => throw new ArgumentOutOfRangeException(
            nameof(failure), failure, "No wire form is defined for this failure reason."),
    };
}

/// <summary>Groups the session outcome, failure reason and span status for one terminal write.</summary>
internal readonly record struct RecoveryOutcome(string Value, string? FailureReason, ActivityStatusCode? Status)
{
    /// <summary>
    /// No exit path classified the session. The pessimistic starting value, so an exit added
    /// later that classifies nothing shows up in the data instead of a value that looks correct.
    /// </summary>
    internal static RecoveryOutcome Unknown { get; } = new("unknown", null, null);

    internal static RecoveryOutcome Ok { get; } = new("ok", null, ActivityStatusCode.Ok);

    /// <summary>
    /// The user stopped the session. <see cref="Status"/> is null so the span is left Unset:
    /// cancelling is not an error and <see cref="ActivityStatusCode"/> has no Cancelled member.
    /// </summary>
    internal static RecoveryOutcome Cancelled { get; } = new("cancelled", null, null);

    /// <summary>Creates an error outcome from a validated failure reason.</summary>
    internal static RecoveryOutcome Error(RecoveryFailure failure) =>
        new("error", failure.Wire(), ActivityStatusCode.Error);
}
