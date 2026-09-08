// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Cmdlets.Compute;

/// <summary>
/// Registers Compute format data with the current PowerShell runspace when the module is imported.
/// Format views are defined in <see cref="ComputeTableViews"/> using shared Core.Abstractions types
/// and rendered to ps1xml content by <see cref="TableFormatPs1XmlBuilder"/>.
/// </summary>
public sealed class ComputeModuleInitializer : IModuleAssemblyInitializer
{
    public void OnImport()
    {
        var ps1xml = TableFormatPs1XmlBuilder.Build(ComputeTableViews.All);

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"Azclips.Compute.{Guid.NewGuid():N}.format.ps1xml");

        File.WriteAllText(tempPath, ps1xml);

        using var ps = System.Management.Automation.PowerShell.Create();
        ps.Runspace = Runspace.DefaultRunspace;
        ps.AddCommand("Update-FormatData")
          .AddParameter("PrependPath", tempPath);
        ps.Invoke();
    }
}
