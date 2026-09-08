// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Models;

/// <summary>
/// Defines the default PowerShell table-format view for <see cref="VirtualMachine"/>.
/// This class implements <see cref="ITableViewDefinition"/> from <c>Core.Abstractions</c> so that
/// the column layout can be shared and generated consistently via <see cref="FormatXmlGenerator"/>.
/// </summary>
public sealed class VirtualMachineTableView : ITableViewDefinition
{
    /// <inheritdoc />
    public IReadOnlyList<TableColumnDefinition> GetColumns() =>
    [
        TableColumnDefinition.FromProperty("Name"),
        TableColumnDefinition.FromScript(
            "ResourceGroupName",
            "if ($null -ne $_.Id) { [regex]::Match($_.Id, '(?i)/resourceGroups/([^/]+)').Groups[1].Value }"),
        TableColumnDefinition.FromProperty("Location"),
        TableColumnDefinition.FromScript(
            "Size",
            "if ($null -ne $_.Properties -and $null -ne $_.Properties.HardwareProfile) { $_.Properties.HardwareProfile.VmSize }"),
        TableColumnDefinition.FromScript(
            "ProvisioningState",
            "if ($null -ne $_.Properties) { $_.Properties.ProvisioningState }"),
    ];
}
