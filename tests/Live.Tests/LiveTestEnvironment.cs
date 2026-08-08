namespace Live.Tests;

/// <summary>
/// Configuration for the live tests, supplied by the surrounding scripts through
/// environment variables.
/// <para>
/// These tests talk to the real Azure Instance Metadata Service, which is only
/// reachable from inside the VM under test. They are therefore skipped unless
/// <see cref="EnableVariable"/> is set, so an ordinary <c>dotnet test</c> run on a
/// developer machine or in PR CI stays green without any Azure access.
/// </para>
/// </summary>
internal static class LiveTestEnvironment
{
    /// <summary>Opt-in switch. Nothing here runs unless this is set to 1/true.</summary>
    public const string EnableVariable = "AZCLIPS_LIVE_TEST";

    /// <summary>Path to the bundled <c>az</c> host under test.</summary>
    public const string AzVariable = "AZCLIPS_LIVE_AZ";

    public const string SubscriptionIdVariable = "AZCLIPS_LIVE_SUBSCRIPTION_ID";
    public const string VmNameVariable = "AZCLIPS_LIVE_VM_NAME";
    public const string ResourceGroupVariable = "AZCLIPS_LIVE_RESOURCE_GROUP";
    public const string UamiClientIdVariable = "AZCLIPS_LIVE_UAMI_CLIENT_ID";
    public const string UamiResourceIdVariable = "AZCLIPS_LIVE_UAMI_RESOURCE_ID";
    public const string UamiObjectIdVariable = "AZCLIPS_LIVE_UAMI_OBJECT_ID";

    /// <summary>
    /// Absolute path to the CLI under test. Resolved eagerly because the test host's
    /// working directory is its own output folder, not the bundle root.
    /// </summary>
    public static string Az => Path.GetFullPath(Require(AzVariable));
    public static string SubscriptionId => Require(SubscriptionIdVariable);
    public static string VmName => Require(VmNameVariable);
    public static string ResourceGroup => Require(ResourceGroupVariable);
    public static string UamiClientId => Require(UamiClientIdVariable);
    public static string UamiResourceId => Require(UamiResourceIdVariable);
    public static string UamiObjectId => Require(UamiObjectIdVariable);

    public static bool IsEnabled
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(EnableVariable);
            return string.Equals(value, "1", StringComparison.Ordinal)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Returns the reason these tests cannot run, or <c>null</c> when everything
    /// needed is present. Used to populate xUnit's <c>Skip</c> so a missing value
    /// reads as "not configured" rather than a confusing assertion failure.
    /// </summary>
    public static string? GetSkipReason(params string[] requiredVariables)
    {
        if (!IsEnabled)
        {
            return $"Live test: set {EnableVariable}=1 to run (needs a provisioned Azure VM). "
                + "Use scripts/mi-live-test.sh.";
        }

        var missing = requiredVariables
            .Concat([AzVariable, SubscriptionIdVariable, VmNameVariable, ResourceGroupVariable])
            .Distinct(StringComparer.Ordinal)
            .Where(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
            .ToArray();

        return missing.Length == 0
            ? null
            : $"Live test: missing required environment variable(s): {string.Join(", ", missing)}.";
    }

    private static string Require(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Environment variable {name} is required by the live tests but was not set.");
        }

        return value;
    }
}
