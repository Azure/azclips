// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

internal sealed class NewVirtualMachineProcessor_Customized : ComputeProcessorBase<object, VirtualMachine>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string VmSize { get; set; } = string.Empty;
    public string AdminUsername { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string OsDiskCreateOption { get; set; } = "FromImage";
    public string ImageId { get; set; } = string.Empty;
    public string ImagePublisher { get; set; } = "MicrosoftWindowsServer";
    public string ImageOffer { get; set; } = "WindowsServer";
    public string ImageSku { get; set; } = "2022-datacenter-azure-edition";
    public string ImageVersion { get; set; } = "latest";
    public string NetworkInterfaceId { get; set; } = string.Empty;
    public string SecurityType { get; set; } = "TrustedLaunch";
    public bool SecureBootEnabled { get; set; } = true;
    public bool VTpmEnabled { get; set; } = true;
    public bool BootDiagnosticsEnabled { get; set; }
    public bool DisableBootDiagnostics { get; set; }
    public string BootDiagnosticsStorageUri { get; set; } = string.Empty;
    public string BootDiagnosticsStorageAccountUri { get; set; } = string.Empty;
    public string IdentityType { get; set; } = string.Empty;
    public bool SystemAssignedIdentity { get; set; }
    public List<string> UserAssignedIdentityIds { get; set; } = [];

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{Name}?api-version=2025-11-01";

    protected override object? CreateRequestBody()
    {
        Dictionary<string, object?> storageProfile = new(StringComparer.OrdinalIgnoreCase)
        {
            ["imageReference"] = CreateImageReference(),
        };

        if (!string.IsNullOrWhiteSpace(OsDiskCreateOption))
        {
            storageProfile["osDisk"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["createOption"] = OsDiskCreateOption,
            };
        }

        Dictionary<string, object?> properties = new(StringComparer.OrdinalIgnoreCase)
        {
            ["hardwareProfile"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["vmSize"] = VmSize,
            },
            ["storageProfile"] = storageProfile,
            ["osProfile"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["computerName"] = Name,
                ["adminUsername"] = AdminUsername,
                ["adminPassword"] = AdminPassword,
                ["windowsConfiguration"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            },
            ["networkProfile"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["networkInterfaces"] = new[]
                {
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["id"] = NetworkInterfaceId,
                        ["primary"] = true,
                    },
                },
            },
            ["diagnosticsProfile"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["bootDiagnostics"] = CreateBootDiagnostics(),
            },
        };

        if (!IsStandardSecurityType())
        {
            properties["securityProfile"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["securityType"] = NormalizeSecurityType(SecurityType),
                ["uefiSettings"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["secureBootEnabled"] = SecureBootEnabled,
                    ["vTpmEnabled"] = VTpmEnabled,
                },
            };
        }

        Dictionary<string, object?> body = new(StringComparer.OrdinalIgnoreCase)
        {
            ["location"] = Location,
            ["properties"] = properties,
        };

        if (CreateIdentity() is { } identity)
        {
            body["identity"] = identity;
        }

        return body;
    }

    private Dictionary<string, object?> CreateImageReference()
    {
        if (!string.IsNullOrWhiteSpace(ImageId))
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = ImageId,
            };
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["publisher"] = ImagePublisher,
            ["offer"] = ImageOffer,
            ["sku"] = ImageSku,
            ["version"] = ImageVersion,
        };
    }

    private Dictionary<string, object?> CreateBootDiagnostics()
    {
        string storageUri = !string.IsNullOrWhiteSpace(BootDiagnosticsStorageUri)
            ? BootDiagnosticsStorageUri
            : BootDiagnosticsStorageAccountUri;
        bool enabled = !DisableBootDiagnostics && (BootDiagnosticsEnabled || !string.IsNullOrWhiteSpace(storageUri));

        Dictionary<string, object?> bootDiagnostics = new(StringComparer.OrdinalIgnoreCase)
        {
            ["enabled"] = enabled,
        };

        if (enabled && !string.IsNullOrWhiteSpace(storageUri))
        {
            bootDiagnostics["storageUri"] = storageUri;
        }

        return bootDiagnostics;
    }

    private Dictionary<string, object?>? CreateIdentity()
    {
        bool wantsSystemAssigned = SystemAssignedIdentity || IdentityType.Contains("SystemAssigned", StringComparison.OrdinalIgnoreCase);
        bool wantsUserAssigned = UserAssignedIdentityIds.Count > 0 || IdentityType.Contains("UserAssigned", StringComparison.OrdinalIgnoreCase);
        if (!wantsSystemAssigned && !wantsUserAssigned)
        {
            return null;
        }

        string type = (wantsSystemAssigned, wantsUserAssigned) switch
        {
            (true, true) => "SystemAssigned,UserAssigned",
            (true, false) => "SystemAssigned",
            _ => "UserAssigned",
        };

        Dictionary<string, object?> identity = new(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = type,
        };

        if (UserAssignedIdentityIds.Count > 0)
        {
            identity["userAssignedIdentities"] = UserAssignedIdentityIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(id => id, _ => (object?)new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        }

        return identity;
    }

    private bool IsStandardSecurityType() => string.Equals(SecurityType, "Standard", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeSecurityType(string securityType)
    {
        if (string.Equals(securityType, "trustedlaunch", StringComparison.OrdinalIgnoreCase))
        {
            return "TrustedLaunch";
        }

        if (string.Equals(securityType, "standard", StringComparison.OrdinalIgnoreCase))
        {
            return "Standard";
        }

        return string.IsNullOrWhiteSpace(securityType) ? "TrustedLaunch" : securityType;
    }

    protected override Func<HttpResponseMessage, string?> GetFinalUri => response => response.GetFirstHeader("Location");
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}