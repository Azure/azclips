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
}

/// <summary>Outcome of one data boundary lookup.</summary>
/// <param name="Boundary">The parsed boundary.</param>
/// <param name="RawValue">
/// Exactly what ARM returned, kept even when parsing fails so telemetry surfaces a value we
/// don't recognize yet rather than silently discarding it.
/// </param>
/// <param name="TenantId">The tenant the lookup was made for, when one was resolved.</param>
/// <param name="FailureReason">A short, non-sensitive reason when <see cref="Boundary"/> is Unknown; otherwise null.</param>
internal readonly record struct DataBoundaryResult(
    DataBoundary Boundary,
    string? RawValue,
    string? TenantId,
    string? FailureReason)
{
    public static DataBoundaryResult Resolved(DataBoundary boundary, string? rawValue, string? tenantId) =>
        new(boundary, rawValue, tenantId, FailureReason: null);

    public static DataBoundaryResult Failed(string reason, string? tenantId = null, string? rawValue = null) =>
        new(DataBoundary.Unknown, rawValue, tenantId, reason);

    /// <summary>Lower-case wire form for tags and trace entries: <c>eu</c>, <c>global</c>, or <c>unknown</c>.</summary>
    public string TagValue => Boundary switch
    {
        DataBoundary.Eu => "eu",
        DataBoundary.Global => "global",
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
