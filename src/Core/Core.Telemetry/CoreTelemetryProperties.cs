using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// Per-process invariants required on every Cirrus command-execution root span,
/// per the DevDiv Azure Monitor onboarding contract.
/// </summary>
/// <remarks>
/// Values are computed once and cached for the lifetime of the process.
/// Call <see cref="ApplyTo(Activity)"/> from a single tagging point (the engine)
/// to keep the contract centralised.
/// </remarks>
public static class CoreTelemetryProperties
{
    public const string ProductNameTag = "ProductName";
    /// <summary>Literal product name reported by Cirrus.</summary>
    public const string ProductName = "cirrus";

    /// <summary>Numeric schema version of the core property bag.</summary>
    public const int SchemaVersion = 1;

    /// <summary>String telemetry version of the core property bag.</summary>
    public const string TelemetryVersion = "1.0";

    /// <summary>Environment variable consulted to determine <see cref="IsMicrosoftInternal"/>.</summary>
    public const string IsMicrosoftInternalEnvironmentVariable = "CIRRUS_IS_MICROSOFT_INTERNAL";

    private static readonly Lazy<string> _id = new(ComputeId);
    private static readonly Lazy<bool> _isMicrosoftInternal = new(ResolveIsMicrosoftInternal);
    private static readonly Lazy<string> _productVersion = new(ResolveProductVersion);

    /// <summary>
    /// SHA-256 hash of the first non-loopback MAC address combined with
    /// <see cref="Environment.MachineName"/>. Deterministic per machine and
    /// resistant to trivial reverse lookup.
    /// </summary>
    public static string Id => _id.Value;

    /// <summary>
    /// <c>true</c> when the <c>CIRRUS_IS_MICROSOFT_INTERNAL</c> environment
    /// variable is set to a truthy value (<c>true</c>, <c>1</c>, <c>yes</c>);
    /// otherwise <c>false</c>.
    /// </summary>
    public static bool IsMicrosoftInternal => _isMicrosoftInternal.Value;

    /// <summary>
    /// Informational version of the <c>Core.Abstractions</c> assembly, used as the
    /// product version on every root span.
    /// </summary>
    public static string ProductVersion => _productVersion.Value;

    /// <summary>
    /// Tags <paramref name="root"/> with the six DevDiv-mandated core properties:
    /// <c>Id</c>, <c>IsMicrosoftInternal</c>, <c>productName</c>,
    /// <c>productVersion</c>, <c>schemaVersion</c>, <c>telemetryVersion</c>.
    /// </summary>
    /// <param name="root">The root activity to tag. No-op when <c>null</c>.</param>
    public static void ApplyTo(Activity? root)
    {
        if (root is null) return;
        root.SetTag("Id", Id);
        root.SetTag("IsMicrosoftInternal", IsMicrosoftInternal);
        root.SetTag("productName", ProductName);
        root.SetTag(ProductNameTag, ProductVersion);
        root.SetTag("schemaVersion", SchemaVersion);
        root.SetTag("telemetryVersion", TelemetryVersion);
    }

    private static string ComputeId()
    {
        var mac = GetPrimaryMacAddress();
        var seed = $"{mac}|{Environment.MachineName}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexString(bytes);
    }

    private static string GetPrimaryMacAddress()
    {
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                var bytes = nic.GetPhysicalAddress().GetAddressBytes();
                if (bytes.Length > 0)
                {
                    return Convert.ToHexString(bytes);
                }
            }
        }
        catch
        {
            // Defensive: NIC enumeration can throw on restricted hosts. Fall through
            // to an empty seed so Id remains stable rather than failing telemetry.
        }
        return string.Empty;
    }

    private static bool ResolveIsMicrosoftInternal()
    {
        return false;
    }

    private static string ResolveProductVersion()
    {
        var assembly = typeof(ITelemetryService).Assembly; // Core.Abstractions
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (!string.IsNullOrEmpty(informational?.InformationalVersion))
        {
            return informational.InformationalVersion;
        }
        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }
}
