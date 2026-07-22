// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Core.Telemetry;

public static class TelemetryPropertyResolver
{
    public static readonly string ProductName = ResolveProductName();

    private static readonly Lazy<string> _installationId = new(ComputeInstallationId);    
    private static readonly Lazy<string> _productVersion = new(ResolveProductVersion);
    private static readonly Lazy<string> _telemetryVersion = new(ResolveTelemetryVersion);

    public static string InstallationId => _installationId.Value;    
    public static string ProductVersion => _productVersion.Value;
    public static string TelemetryVersion => _telemetryVersion.Value;

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

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (!string.IsNullOrEmpty(informational?.InformationalVersion))
        {
            return informational.InformationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    private static string ResolveProductName()
    {
        var product = typeof(TelemetryPropertyResolver).Assembly
            .GetCustomAttribute<AssemblyProductAttribute>()?.Product;

        return !string.IsNullOrEmpty(product) ? product : "Azclips";
    }

    private static string ResolveTelemetryVersion()
    {
        var version = typeof(TelemetryPropertyResolver).Assembly.GetName().Version;
        return version is not null ? version.ToString() : "unknown";
    }
}
