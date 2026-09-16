using System.Text.RegularExpressions;

namespace Core.Agent.Providers;

/// <summary>
/// Why a data boundary lookup has no boundary to report. The closed half of the
/// <c>recovery.dataBoundary</c> vocabulary.
/// </summary>
/// <remarks>
/// An enum rather than strings at the call sites because this ships as a telemetry dimension:
/// <c>eng-docs/spec/telemetry.md</c> promises a fixed vocabulary and never a message.
/// Interpolating a caught exception's type into it would turn that promise into an open set —
/// every new exception type the runtime throws becoming a new dimension value. Going through an
/// enum makes that impossible to express rather than merely discouraged. The only no-answer value
/// carrying a variable is <see cref="DataBoundaryResult.FromArmStatus"/>, whose range is the
/// bounded set of HTTP status codes.
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
}

/// <summary>Outcome of one data boundary lookup.</summary>
/// <remarks>
/// One field, deliberately. A boundary plus a separate failure reason was two tags for one answer,
/// and every startup path that wrote one without the other was a hole in the data that the type
/// could not prevent. A single field cannot be half-written, so the holes stop being expressible.
/// </remarks>
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

    /// <summary>
    /// ARM answered. Its value passes through — <c>eu</c>, <c>global</c>, or whatever ARM adds
    /// later — lowercased.
    /// </summary>
    /// <remarks>
    /// This is the only untrusted string in the vocabulary, so it is the only one that is
    /// sanitised: <see cref="NoAnswer"/> emits a closed enum's wire form and
    /// <see cref="FromArmStatus"/> builds its value from an HTTP status code, both trusted by
    /// construction and neither re-validated here.
    ///
    /// <para>What this rejects is shape, not meaning. A remote string landing in a telemetry
    /// dimension is unbounded cardinality and a content-injection path, so anything outside the
    /// shape we are willing to export becomes <c>unexpected_value</c>. An unfamiliar but
    /// well-shaped value is passed through instead: discovering one is the reason this probe
    /// exists.</para>
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
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "No wire form defined."),
    };

    // 32 characters is well past any plausible boundary name and far short of anything that could
    // carry a payload.
    [GeneratedRegex("^[a-z0-9_-]{1,32}$")]
    private static partial Regex PlausibleWireToken();
}
