// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Core.Agent.Telemetry;

/// <summary>
/// Closed vocabulary for <c>properties.recovery.failureReason</c>.
/// </summary>
/// <remarks>
/// <para>The tag previously carried <c>ex.GetType().Name</c> on the host path and the SDK's raw
/// <c>ErrorType</c> on the model path. Both are sets defined outside this build — one by whatever
/// the runtime happens to throw, the other by the service — so the dimension had unbounded
/// cardinality and no value list anyone could review.</para>
///
/// <para>The model half cannot be enumerated at all: <c>SessionErrorData.ErrorType</c> is a
/// required string passed through from the service, with no constants behind it. The three
/// model-side members below are therefore an allowlist of the values this repository has actually
/// observed, and anything else becomes <see cref="SdkOther"/>. Adding a member is a one-line
/// change once the data shows a value is worth separating.</para>
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

/// <summary>
/// Terminal state of an <c>Agent.Session</c>: the <c>properties.recovery.outcome</c> value, the
/// failure reason that accompanies an error, and the span status to set.
/// </summary>
/// <remarks>
/// The three travel together so <see cref="AgentMode.AgentModeEntry.RunSessionAsync"/> can record
/// them at one point. They used to be written at each exit separately, which meant a new exit path
/// exported a session with no outcome at all.
/// </remarks>
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

    /// <summary>
    /// Takes the enum rather than its wire form, so the closed vocabulary is enforced by the type
    /// rather than by every call site remembering to call <see cref="RecoveryFailureExtensions.Wire"/>.
    /// An earlier draft took a string to keep one reverse-validation revert type-checkable; that
    /// optimised a production signature for a hypothetical experiment and left the chokepoint the
    /// enum exists to create open to any string, including the <c>ex.GetType().Name</c> this PR
    /// removed.
    /// </summary>
    internal static RecoveryOutcome Error(RecoveryFailure failure) =>
        new("error", failure.Wire(), ActivityStatusCode.Error);
}
