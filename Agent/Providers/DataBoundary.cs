namespace Core.Agent.Providers;

/// <summary>
/// The caller tenant's ARM data boundary, which decides where their prompts may be
/// processed.
/// </summary>
/// <remarks>
/// <see cref="Unknown"/> is deliberately distinct from <see cref="Global"/>. ARM reports
/// <c>Global</c> for a tenant that has no boundary resource, which is a real answer, whereas
/// Unknown means we never got one. Collapsing the two would let a lookup failure read as
/// permission to process an EU tenant's data outside the EU once routing lands.
/// </remarks>
internal enum DataBoundary
{
    /// <summary>No authoritative answer: no tenant, a failed call, or an unrecognized value.</summary>
    Unknown,

    /// <summary>ARM reported <c>Global</c>, the default for a tenant with no boundary configured.</summary>
    Global,

    /// <summary>ARM reported <c>EU</c>; the tenant is opted into the EU Data Boundary.</summary>
    Eu,

    /// <summary>
    /// The lookup does not apply to this session, so it was never attempted — today that means the
    /// <c>github</c> provider, whose traffic goes to GitHub rather than the Microsoft endpoint.
    /// </summary>
    /// <remarks>
    /// A telemetry state, not a boundary. It exists so a deliberately skipped lookup is
    /// distinguishable in the data from one that ran and failed (<see cref="Unknown"/>) and from a
    /// tag that never survived export. It must never appear in a routing decision: when routing
    /// lands, this and <see cref="Unknown"/> both have to be handled explicitly, and neither may
    /// fall through to <see cref="Global"/>.
    /// </remarks>
    NotApplicable,
}

/// <summary>
/// The closed vocabulary for <see cref="DataBoundaryResult.FailureReason"/>.
/// </summary>
/// <remarks>
/// An enum rather than strings at the call sites because <c>recovery.dataBoundary.failureReason</c>
/// is a telemetry dimension: <c>eng-docs/spec/telemetry.md</c> promises a fixed vocabulary and never
/// a message. Interpolating a caught exception's type into the reason silently turned that promise
/// into an open set — every new exception type the runtime throws would become a new dimension
/// value. Going through an enum makes that impossible to express instead of merely discouraged; the
/// only reason carrying a variable is <see cref="DataBoundaryResult.FailedWithArmStatus"/>, whose
/// range is the bounded set of HTTP status codes.
/// </remarks>
internal enum DataBoundaryFailure
{
    TimeoutReadingProfile,
    ProfileUnavailable,
    NoActiveTenant,
    TimeoutAcquiringToken,
    TokenUnavailable,
    ArmRequestFailed,
    ArmTimeout,
    EmptyResponse,
    MalformedResponse,
    MissingDataBoundary,
    TenantMismatch,
    UnrecognizedValue,

    /// <summary>The provider is explicitly <c>github</c>, which never calls ARM.</summary>
    ProviderGithub,

    /// <summary>
    /// The configured provider is neither <c>builtin</c> nor <c>github</c>. Startup rejects it
    /// moments later, but the boundary tag is written first, so it needs a value of its own rather
    /// than borrowing the one that means "this is a GitHub session".
    /// </summary>
    ProviderUnsupported,
}

/// <summary>Outcome of one data boundary lookup.</summary>
/// <param name="Boundary">The parsed boundary.</param>
/// <param name="RawValue">
/// Exactly what ARM returned, kept even when parsing fails so telemetry surfaces a value we
/// don't recognize yet rather than silently discarding it.
/// </param>
/// <param name="TenantId">The tenant the lookup was made for, when one was resolved.</param>
/// <param name="FailureReason">
/// A short, non-sensitive reason whenever <see cref="Boundary"/> is not a resolved boundary — set
/// for both <see cref="DataBoundary.Unknown"/> (we tried and could not determine it) and
/// <see cref="DataBoundary.NotApplicable"/> (we did not try). Null once a boundary is resolved.
/// </param>
internal readonly record struct DataBoundaryResult(
    DataBoundary Boundary,
    string? RawValue,
    string? TenantId,
    string? FailureReason)
{
    public static DataBoundaryResult Resolved(DataBoundary boundary, string? rawValue, string? tenantId) =>
        new(boundary, rawValue, tenantId, FailureReason: null);

    public static DataBoundaryResult Failed(
        DataBoundaryFailure reason, string? tenantId = null, string? rawValue = null) =>
        new(DataBoundary.Unknown, rawValue, tenantId, Wire(reason));

    /// <summary>
    /// ARM answered, but not with success. The status is part of the reason because it is what
    /// separates "throttled" from "no permission" from "ARM is down", and its range is bounded.
    /// </summary>
    public static DataBoundaryResult FailedWithArmStatus(int statusCode, string? tenantId) =>
        new(DataBoundary.Unknown, RawValue: null, tenantId, $"arm_status_{statusCode}");

    /// <summary>
    /// The lookup was deliberately not attempted. Distinct from <see cref="Failed"/> so telemetry can
    /// tell "does not apply" from "we tried and could not determine it".
    /// </summary>
    public static DataBoundaryResult NotApplicable(DataBoundaryFailure reason) =>
        new(DataBoundary.NotApplicable, RawValue: null, TenantId: null, Wire(reason));

    /// <summary>Wire form of a reason. Exhaustive, so a new member cannot ship without one.</summary>
    private static string Wire(DataBoundaryFailure reason) => reason switch
    {
        DataBoundaryFailure.TimeoutReadingProfile => "timeout_reading_profile",
        DataBoundaryFailure.ProfileUnavailable => "profile_unavailable",
        DataBoundaryFailure.NoActiveTenant => "no_active_tenant",
        DataBoundaryFailure.TimeoutAcquiringToken => "timeout_acquiring_token",
        DataBoundaryFailure.TokenUnavailable => "token_unavailable",
        DataBoundaryFailure.ArmRequestFailed => "arm_request_failed",
        DataBoundaryFailure.ArmTimeout => "timeout",
        DataBoundaryFailure.EmptyResponse => "empty_response",
        DataBoundaryFailure.MalformedResponse => "malformed_response",
        DataBoundaryFailure.MissingDataBoundary => "missing_data_boundary",
        DataBoundaryFailure.TenantMismatch => "tenant_mismatch",
        DataBoundaryFailure.UnrecognizedValue => "unrecognized_value",
        DataBoundaryFailure.ProviderGithub => "provider_github",
        DataBoundaryFailure.ProviderUnsupported => "provider_unsupported",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "No wire form defined."),
    };

    /// <summary>Lower-case wire form for tags and trace entries: <c>eu</c>, <c>global</c>, <c>not_applicable</c>, or <c>unknown</c>.</summary>
    public string TagValue => Boundary switch
    {
        DataBoundary.Eu => "eu",
        DataBoundary.Global => "global",
        DataBoundary.NotApplicable => "not_applicable",
        _ => "unknown",
    };

    /// <summary>
    /// Maps an ARM <c>properties.dataBoundary</c> value. Only the two documented boundaries are
    /// accepted; <c>NotDefined</c>, an empty value, and anything added later fall through to
    /// Unknown so a new value is never mistaken for one we understand.
    /// </summary>
    public static DataBoundary Parse(string? rawValue)
    {
        if (string.Equals(rawValue, "EU", StringComparison.OrdinalIgnoreCase))
            return DataBoundary.Eu;

        if (string.Equals(rawValue, "Global", StringComparison.OrdinalIgnoreCase))
            return DataBoundary.Global;

        return DataBoundary.Unknown;
    }
}
