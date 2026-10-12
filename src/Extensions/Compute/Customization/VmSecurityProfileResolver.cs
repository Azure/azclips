// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

using Core.Abstractions;

internal static class VmSecurityProfileResolver
{
    internal static ResolvedVmSecurityProfile Resolve(
        string? requestedSecurityType,
        bool useTrustedLaunchDefaults,
        string? osDiskSecurityEncryptionType,
        string? osDiskSecureVMDiskEncryptionSet,
        bool? enableSecureBoot,
        bool? enableVtpm,
        string subscriptionId,
        string resourceGroupName)
    {
        var securityProfileSettings = ResolveSecurityProfileSettings(
            requestedSecurityType, useTrustedLaunchDefaults, enableSecureBoot, enableVtpm);
        ValidateConfidentialVmDiskEncryption(
            securityProfileSettings.SecurityType,
            osDiskSecurityEncryptionType,
            osDiskSecureVMDiskEncryptionSet);

        return new ResolvedVmSecurityProfile
        {
            SecurityType = securityProfileSettings.SecurityType,
            SecureBootEnabled = securityProfileSettings.SecureBootEnabled,
            VTpmEnabled = securityProfileSettings.VTpmEnabled,
            OsDiskSecurityEncryptionType = osDiskSecurityEncryptionType,
            OsDiskSecureVMDiskEncryptionSetId = ResolveDiskEncryptionSetId(
                osDiskSecureVMDiskEncryptionSet,
                subscriptionId,
                resourceGroupName)
        };
    }

    private static string? ResolveDiskEncryptionSetId(
        string? diskEncryptionSet,
        string subscriptionId,
        string resourceGroupName)
    {
        if (string.IsNullOrWhiteSpace(diskEncryptionSet))
        {
            return null;
        }

        var value = diskEncryptionSet.Trim();
        if (value.StartsWith("/subscriptions/", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                VirtualMachineIdResolver.ParseResourceId(
                    value,
                    "Microsoft.Compute",
                    "diskEncryptionSets",
                    "Disk encryption set",
                    "diskEncryptionSetName");
            }
            catch (InvalidOperationException ex)
            {
                throw AzclipsErrorException.UsageError(ex.Message);
            }

            return value;
        }

        return $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/diskEncryptionSets/{value}";
    }

    private static void ValidateConfidentialVmDiskEncryption(
        string? resolvedSecurityType,
        string? osDiskSecurityEncryptionType,
        string? osDiskSecureVMDiskEncryptionSet)
    {
        var hasSecurityEncryptionType = !string.IsNullOrWhiteSpace(osDiskSecurityEncryptionType);
        var hasDiskEncryptionSet = !string.IsNullOrWhiteSpace(osDiskSecureVMDiskEncryptionSet);

        if (!string.Equals(resolvedSecurityType, "ConfidentialVM", StringComparison.OrdinalIgnoreCase))
        {
            if (hasSecurityEncryptionType)
            {
                throw AzclipsErrorException.UsageError("OS disk security encryption type can only be specified when security type is ConfidentialVM.");
            }

            if (hasDiskEncryptionSet)
            {
                throw AzclipsErrorException.UsageError("OS disk secure VM disk encryption set can only be specified when security type is ConfidentialVM and OS disk security encryption type is DiskWithVMGuestState.");
            }

            return;
        }

        if (!hasSecurityEncryptionType)
        {
            throw AzclipsErrorException.UsageError("OS disk security encryption type is required when security type is ConfidentialVM.");
        }

        if (!IsOsDiskSecurityEncryptionType(osDiskSecurityEncryptionType!))
        {
            throw AzclipsErrorException.UsageError("OS disk security encryption type must be one of: VMGuestStateOnly, DiskWithVMGuestState, NonPersistedTPM.");
        }

        if (hasDiskEncryptionSet
            && !string.Equals(osDiskSecurityEncryptionType, "DiskWithVMGuestState", StringComparison.OrdinalIgnoreCase))
        {
            throw AzclipsErrorException.UsageError("OS disk secure VM disk encryption set can only be specified when OS disk security encryption type is DiskWithVMGuestState.");
        }
    }

    private static bool IsOsDiskSecurityEncryptionType(string value)
    {
        return value.Equals("VMGuestStateOnly", StringComparison.OrdinalIgnoreCase)
            || value.Equals("DiskWithVMGuestState", StringComparison.OrdinalIgnoreCase)
            || value.Equals("NonPersistedTPM", StringComparison.OrdinalIgnoreCase);
    }

    private static (string? SecurityType, bool? SecureBootEnabled, bool? VTpmEnabled) ResolveSecurityProfileSettings(
        string? requestedSecurityType,
        bool useTrustedLaunchDefaults,
        bool? enableSecureBoot,
        bool? enableVtpm)
    {
        var hasUefiOverride = enableSecureBoot.HasValue || enableVtpm.HasValue;

        if (string.IsNullOrWhiteSpace(requestedSecurityType))
        {
            if (!useTrustedLaunchDefaults)
            {
                if (hasUefiOverride)
                {
                    throw AzclipsErrorException.UsageError("Specify security type TrustedLaunch or ConfidentialVM when setting secure boot or vTPM.");
                }

                return (null, null, null);
            }

            return ("TrustedLaunch", enableSecureBoot ?? true, enableVtpm ?? true);
        }

        if (requestedSecurityType.Equals("Standard", StringComparison.OrdinalIgnoreCase))
        {
            if (hasUefiOverride)
            {
                throw AzclipsErrorException.UsageError("Secure boot and vTPM can only be set when security type is TrustedLaunch or ConfidentialVM.");
            }

            return ("Standard", null, null);
        }

        if (IsTrustedLaunchOrConfidentialVm(requestedSecurityType))
        {
            return (requestedSecurityType, enableSecureBoot ?? true, enableVtpm ?? true);
        }

        throw AzclipsErrorException.UsageError("Security type must be one of: TrustedLaunch, ConfidentialVM, Standard.");
    }

    private static bool IsTrustedLaunchOrConfidentialVm(string securityType)
    {
        return securityType.Equals("TrustedLaunch", StringComparison.OrdinalIgnoreCase)
            || securityType.Equals("ConfidentialVM", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class ResolvedVmSecurityProfile
{
    public string? SecurityType { get; init; }
    public bool? SecureBootEnabled { get; init; }
    public bool? VTpmEnabled { get; init; }
    public string? OsDiskSecurityEncryptionType { get; init; }
    public string? OsDiskSecureVMDiskEncryptionSetId { get; init; }
}
