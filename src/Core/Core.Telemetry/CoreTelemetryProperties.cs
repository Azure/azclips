using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// Per-process invariants required on every Cirrus command-execution root span.
/// </summary>
/// <remarks>
/// Values are computed once and cached for the lifetime of the process.
/// Call <see cref="ApplyStaticProperties"/> from a single tagging point (the engine)
/// to keep the contract centralised.
/// </remarks>
public static class CoreTelemetryProperties
{
    public const string IdTag = "Id";
    public const string MicrosoftInternalTag = "IsMicrosoftInternal";
    public const string ProductNameTag = "productName";
    public const string ProductVersionTag = "productVersion";
    public const string SchemaVersionTag = "schemaVersion";
    public const string TelemetryVersionTag = "telemetryVersion";
    public const string TotalDurationTag = "totalDuration";
    public const string ClientTypeTag = "clientType";
    public const string ModuleNameTag = "module.name";
    public const string ModuleVersionTag = "module.version";
    public const string EventNameTag = "eventName";
    public const string CommandTag = "command";
    public const string ParameterTag = "parameter";

    public static readonly string ProductName = ResolveProductName();
    public static readonly string SchemaVersion = ResolveSchemaVersion();
    public static readonly string TelemetryVersion = ResolveTelemetryVersion();

    private static readonly Lazy<string> _id = new(ComputeId);
    private static readonly Lazy<bool> _isMicrosoftInternal = new(ResolveIsMicrosoftInternal);
    private static readonly Lazy<string> _productVersion = new(ResolveProductVersion);

    public static string Id => _id.Value;

    public static bool IsMicrosoftInternal => _isMicrosoftInternal.Value;

    public static string ProductVersion => _productVersion.Value;

    public static Activity? ApplyStaticProperties(this Activity? root)
    {
        root?.SetTag(IdTag, Id);
        root?.SetTag(MicrosoftInternalTag, IsMicrosoftInternal);
        root?.SetTag(ProductNameTag, ProductName);
        root?.SetTag(ProductVersionTag, ProductVersion);
        root?.SetTag(SchemaVersionTag, SchemaVersion);
        root?.SetTag(TelemetryVersionTag, TelemetryVersion);
        return root;
    }

    public static Activity? SetDisplayName(this Activity? activity, string extensionName, string commandName)
    {
        activity?.DisplayName = $"{ProductName}.{extensionName}.{commandName}";
        return activity;
    }

    public static Activity? ApplyClientType(this Activity? activity, ClientType clientType)
    {
        activity?.SetTag(ClientTypeTag, clientType);
        return activity;
    }

    public static Activity? ApplyModuleInfo(this Activity? activity, string? name, string? version)
    {
        if (activity is null) return activity;
        if (!string.IsNullOrEmpty(name))
            activity.SetTag(ModuleNameTag, name);
        if (!string.IsNullOrEmpty(version))
            activity.SetTag(ModuleVersionTag, version);
        return activity;
    }

    public static Activity? ApplyTotalDuration(this Activity? activity, TimeSpan elapsed)
    {
        activity?.SetTag(TotalDurationTag, (long)elapsed.TotalMilliseconds);
        return activity;
    }

    public static Activity? ApplyCommandLine(this Activity? activity, string command, string parameter)
    {
        if (activity is null) return activity;
        activity.SetTag(CommandTag, command);
        activity.SetTag(ParameterTag, parameter);
        return activity;
    }

    public static Activity? ApplyEvent(this Activity? activity, string eventName)
    {
        activity?.SetTag(
            EventNameTag,
            $"{ProductName}.{eventName}");
        return activity;
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

    //todo: determine how this is configured. most likely from config maybe?
    private static bool ResolveIsMicrosoftInternal()
    {
        return false;
    }

    private static string ResolveProductVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(CoreTelemetryProperties).Assembly;

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (!string.IsNullOrEmpty(informational?.InformationalVersion))
        {
            return informational.InformationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    private static string ResolveProductName()
    {
        var product = typeof(CoreTelemetryProperties).Assembly
            .GetCustomAttribute<AssemblyProductAttribute>()?.Product;

        return !string.IsNullOrEmpty(product) ? product : "Cirrus";
    }

    private static string ResolveSchemaVersion()
    {
        var value = GetAssemblyMetadata("TelemetrySchemaVersion");
        return !string.IsNullOrEmpty(value) ? value : "1.0";
    }

    private static string ResolveTelemetryVersion()
    {
        var value = GetAssemblyMetadata("TelemetryVersion");
        return !string.IsNullOrEmpty(value) ? value : "1.0";
    }

    private static string? GetAssemblyMetadata(string key)
    {
        foreach (var attr in typeof(CoreTelemetryProperties).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (string.Equals(attr.Key, key, StringComparison.Ordinal))
            {
                return attr.Value;
            }
        }
        return null;
    }
}
