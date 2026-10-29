// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Core.Abstractions;

namespace Core.Telemetry;

public static class TelemetryPropertyResolver
{
    private static readonly Lazy<string> _installationId = new(ComputeInstallationId);
    private static readonly Lazy<string> _productVersion = new(ResolveProductVersion);
    private static readonly Lazy<string> _telemetryVersion = new(ResolveTelemetryVersion);
    private static readonly Lazy<string> _operatingSystemType = new(ResolveOSType);
    private static readonly Lazy<string> _operatingSystemVersion = new(ResolveOSVersion);
    private static readonly Lazy<(string? Name, string? Id)> _linuxDistribution = new(ResolveLinuxDistribution);
    private static readonly Lazy<string> _shellType = new(ResolveShellType);

    public static string InstallationId => _installationId.Value;
    public static string ProductVersion => _productVersion.Value;
    public static string TelemetryVersion => _telemetryVersion.Value;
    public static string OperatingSystemType => _operatingSystemType.Value;
    public static string OperatingSystemVersion => _operatingSystemVersion.Value;
    public static string? DistributionName => _linuxDistribution.Value.Name;
    public static string? DistributionId => _linuxDistribution.Value.Id;
    public static string ShellType => _shellType.Value;

    private static string ComputeInstallationId()
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

    private static string ResolveProductVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(TelemetryPropertyResolver).Assembly;
        return AssemblyVersionProvider.GetVersion(assembly, "0.0.0");
    }

    private static string ResolveTelemetryVersion()
    {
        var version = typeof(TelemetryPropertyResolver).Assembly.GetName().Version;
        return version is not null ? version.ToString() : "unknown";
    }

    private static string ResolveOSType()
    {
        if (OperatingSystem.IsWindows())
            return "Windows";
        if (OperatingSystem.IsLinux())
            return "Linux";
        if (OperatingSystem.IsMacOS())
            return "MacOS";
        return "Unknown";
    }

    public static string ResolveOSVersion()
    {
        return Environment.OSVersion.VersionString;
    }

    private static (string? Name, string? Id) ResolveLinuxDistribution()
    {
        if (!OperatingSystem.IsLinux())
            return default;

        try
        {
            const string NamePrefix = "NAME=";
            const string IdPrefix = "ID=";
            string? name = null;
            string? id = null;

            foreach (var line in File.ReadLines("/etc/os-release"))
            {
                if (line.StartsWith(NamePrefix, StringComparison.Ordinal))
                    name = ParseOsReleaseValue(line[NamePrefix.Length..]);
                else if (line.StartsWith(IdPrefix, StringComparison.Ordinal))
                    id = ParseOsReleaseValue(line[IdPrefix.Length..]);

                if (name is not null && id is not null)
                    break;
            }

            return (name, id);
        }
        catch
        {
            return default;
        }
    }

    private static string? ParseOsReleaseValue(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'')))
        {
            value = value[1..^1];
        }

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string ResolveShellType()
    {
        return ProcessUtils.DetectShell(
            Environment.GetEnvironmentVariable("SHELL"),
            OperatingSystem.IsWindows());
    }
}
