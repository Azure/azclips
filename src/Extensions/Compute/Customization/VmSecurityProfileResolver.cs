// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

internal static class VmSecurityProfileResolver
{
    internal static ResolvedVmSecurityProfile Resolve(
        string? requestedSecurityType,
        bool useTrustedLaunchDefaults,
        string? osDiskSecurityEncryptionType,
        string? osDiskSecureVMDiskEncryptionSet,
        string subscriptionId,
        string resourceGroupName)
    {
        var securityProfileSettings = ResolveSecurityProfileSettings(requestedSecurityType, useTrustedLaunchDefaults);
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
                throw new ArgumentException("OS disk security encryption type can only be specified when security type is ConfidentialVM.", nameof(osDiskSecurityEncryptionType));
            }

            if (hasDiskEncryptionSet)
            {
                throw new ArgumentException("OS disk secure VM disk encryption set can only be specified when security type is ConfidentialVM and OS disk security encryption type is DiskWithVMGuestState.", nameof(osDiskSecureVMDiskEncryptionSet));
            }

            return;
        }

        if (!hasSecurityEncryptionType)
        {
            throw new ArgumentException("OS disk security encryption type is required when security type is ConfidentialVM.", nameof(osDiskSecurityEncryptionType));
        }

        if (!IsOsDiskSecurityEncryptionType(osDiskSecurityEncryptionType!))
        {
            throw new ArgumentException("OS disk security encryption type must be one of: VMGuestStateOnly, DiskWithVMGuestState, NonPersistedTPM.", nameof(osDiskSecurityEncryptionType));
        }

        if (hasDiskEncryptionSet
            && !string.Equals(osDiskSecurityEncryptionType, "DiskWithVMGuestState", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("OS disk secure VM disk encryption set can only be specified when OS disk security encryption type is DiskWithVMGuestState.", nameof(osDiskSecureVMDiskEncryptionSet));
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
        bool useTrustedLaunchDefaults)
    {
        if (string.IsNullOrWhiteSpace(requestedSecurityType))
        {
            return useTrustedLaunchDefaults
                ? ("TrustedLaunch", true, true)
                : (null, null, null);
        }

        if (requestedSecurityType.Equals("Standard", StringComparison.OrdinalIgnoreCase))
        {
            return ("Standard", null, null);
        }

        if (IsTrustedLaunchOrConfidentialVm(requestedSecurityType))
        {
            return (requestedSecurityType, true, true);
        }

        throw new ArgumentException("Security type must be one of: TrustedLaunch, ConfidentialVM, Standard.", nameof(requestedSecurityType));
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
