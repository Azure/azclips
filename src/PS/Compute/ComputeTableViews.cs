// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Cmdlets.Compute;

/// <summary>
/// Declares the PowerShell table format views for all Compute module output types.
/// Each view is built from <see cref="TableFormatView"/> and <see cref="TableColumnDefinition"/>
/// defined in Core.Abstractions so that the same infrastructure can be reused by other modules.
/// </summary>
public static class ComputeTableViews
{
    /// <summary>Default table view for <c>VirtualMachine</c> objects.</summary>
    public static readonly TableFormatView VirtualMachine = new(
        name: "VirtualMachine",
        typeName: "Microsoft.Azure.Azclips.Extensions.Compute.Models.VirtualMachine",
        columns:
        [
            TableColumnDefinition.FromProperty("Name", "Name"),
            TableColumnDefinition.FromScript(
                "ResourceGroupName",
                "if ($null -ne $_.Id) { [regex]::Match($_.Id, '(?i)/resourceGroups/([^/]+)').Groups[1].Value }"),
            TableColumnDefinition.FromProperty("Location", "Location"),
            TableColumnDefinition.FromScript(
                "Size",
                "if ($null -ne $_.Properties -and $null -ne $_.Properties.HardwareProfile) { $_.Properties.HardwareProfile.VmSize }"),
            TableColumnDefinition.FromScript(
                "ProvisioningState",
                "if ($null -ne $_.Properties) { $_.Properties.ProvisioningState }"),
        ]);

    /// <summary>All Compute table views, used by <see cref="ComputeModuleInitializer"/>.</summary>
    public static IReadOnlyList<TableFormatView> All => [VirtualMachine];
}
