using System.Text.RegularExpressions;

namespace Core.Agent.Providers;

/// <summary>Closed reasons why the diagnostic boundary lookup has no answer.</summary>
/// <remarks>
/// Do not use runtime messages as telemetry values.
/// HTTP status outcomes use <see cref="DataBoundaryResult.FromArmStatus"/>.
/// </remarks>
internal enum DataBoundaryUnavailable
{
    /// <summary>
    /// Reserved, and unreachable by design: the startup seam promises never to throw, so the tag
    /// is always written from a classified outcome. If this ever appears in real data, that
    /// contract broke.
    /// </summary>
    NotStarted,

    /// <summary>Provider is <c>github</c>, whose traffic goes to GitHub and never reaches ARM.</summary>
    SkippedGithub,

    /// <summary>
    /// Provider is neither <c>builtin</c> nor <c>github</c>. Startup rejects it moments later, but
    /// the boundary is recorded first, so a typo needs a value of its own rather than borrowing the
    /// one that means "this is a GitHub session".
    /// </summary>
    SkippedUnsupportedProvider,

    /// <summary>No tenant to ask about: signed out, or the profile names none.</summary>
    NoTenant,

    /// <summary>
    /// The profile could not be read at all. Distinct from <see cref="NoTenant"/> because the two
    /// have different fixes: signed out is answered by <c>az login</c>, an unreadable profile is a
    /// storage or permissions problem. Collapsing them would hide a broken install inside a
    /// population of ordinary signed-out users.
    /// </summary>
    ProfileUnavailable,

    /// <summary>Token acquisition ran and failed.</summary>
    NoToken,

    /// <summary>Token acquisition exceeded its budget.</summary>
    TimeoutToken,

    /// <summary>The profile read exceeded the lookup budget.</summary>
    TimeoutProfile,

    /// <summary>The ARM call exceeded the lookup budget.</summary>
    TimeoutArm,

    /// <summary>The ARM call failed at the transport.</summary>
    ArmFailed,

    /// <summary>ARM answered with success but no usable boundary: empty, malformed, or absent.</summary>
    Unreadable,

    /// <summary>ARM echoed a different tenant than the one asked about.</summary>
    TenantMismatch,

    /// <summary>
    /// ARM echoed no tenant at all. Split from <see cref="TenantMismatch"/> because an absent echo
    /// is not an answer for someone else — it is no proof of ownership either way, and merging the
    /// two would report a trimmed response as ARM contradicting us.
    /// </summary>
    TenantUnconfirmed,

    /// <summary>The caller's token was signalled.</summary>
    Cancelled,

    /// <summary>ARM's value failed the sanitiser in <see cref="DataBoundaryResult.FromArm"/>.</summary>
    UnexpectedValue,

    /// <summary>
    /// The lookup threw despite contracting never to. Our own bug rather than an upstream one, and
    /// kept distinct from every real outcome so it cannot hide inside a population of ordinary
    /// failures.
    /// </summary>
    LookupFaulted,
}

/// <summary>One diagnostic value: the boundary answer or the reason it is unavailable.</summary>
internal readonly partial record struct DataBoundaryResult
{
    private DataBoundaryResult(string value, string? tenantId)
    {
        Value = value;
        TenantId = tenantId;
    }

    /// <summary>The single recorded value: ARM's answer normalised, or why there is none.</summary>
    public string Value { get; }

    /// <summary>The tenant the lookup was made for, when one was resolved.</summary>
    public string? TenantId { get; }

    /// <summary>Lowercases ARM's answer and validates its telemetry token format.</summary>
    /// <remarks>
    /// Accept 1-32 lowercase letters, digits, underscores or hyphens.
    /// Preserve unfamiliar values with a valid shape.
    /// Map malformed values to <c>unexpected_value</c>.
    /// </remarks>
    public static DataBoundaryResult FromArm(string? raw, string? tenantId)
    {
        var normalized = raw?.ToLowerInvariant();

        return normalized is not null && PlausibleWireToken().IsMatch(normalized)
            ? new DataBoundaryResult(normalized, tenantId)
            : NoAnswer(DataBoundaryUnavailable.UnexpectedValue, tenantId);
    }

    /// <summary>There is no boundary, and this is why.</summary>
    public static DataBoundaryResult NoAnswer(DataBoundaryUnavailable reason, string? tenantId = null) =>
        new(Wire(reason), tenantId);

    /// <summary>
    /// ARM answered, but not with success. The status is part of the value because it is what
    /// separates "throttled" from "no permission" from "ARM is down", and its range is bounded.
    /// </summary>
    public static DataBoundaryResult FromArmStatus(int statusCode, string? tenantId) =>
        new($"arm_status_{statusCode}", tenantId);

    /// <summary>Wire form of a reason. Exhaustive, so a new member cannot ship without one.</summary>
    private static string Wire(DataBoundaryUnavailable reason) => reason switch
    {
        DataBoundaryUnavailable.NotStarted => "not_started",
        DataBoundaryUnavailable.SkippedGithub => "skipped_github",
        DataBoundaryUnavailable.SkippedUnsupportedProvider => "skipped_unsupported_provider",
        DataBoundaryUnavailable.NoTenant => "no_tenant",
        DataBoundaryUnavailable.ProfileUnavailable => "profile_unavailable",
        DataBoundaryUnavailable.NoToken => "no_token",
        DataBoundaryUnavailable.TimeoutToken => "timeout_token",
        DataBoundaryUnavailable.TimeoutProfile => "timeout_profile",
        DataBoundaryUnavailable.TimeoutArm => "timeout_arm",
        DataBoundaryUnavailable.ArmFailed => "arm_failed",
        DataBoundaryUnavailable.Unreadable => "unreadable",
        DataBoundaryUnavailable.TenantMismatch => "tenant_mismatch",
        DataBoundaryUnavailable.TenantUnconfirmed => "tenant_unconfirmed",
        DataBoundaryUnavailable.Cancelled => "cancelled",
        DataBoundaryUnavailable.UnexpectedValue => "unexpected_value",
        DataBoundaryUnavailable.LookupFaulted => "lookup_faulted",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "No wire form defined."),
    };

    // 32 characters is well past any plausible boundary name and far short of anything that could
    // carry a payload.
    [GeneratedRegex("^[a-z0-9_-]{1,32}$")]
    private static partial Regex PlausibleWireToken();
}
