// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Cmdlets.Tests;

public class GetVMFormattingTests
{
    [Fact]
    public void VirtualMachineView_DisplaysVmSummaryColumns()
    {
        var formatPath = Path.Combine(AppContext.BaseDirectory, "Compute.format.ps1xml");
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "Compute.psd1");
        var view = XDocument.Load(formatPath).Descendants("View").Single();
        var headers = view.Descendants("TableColumnHeader").ToList();
        Assert.All(headers, header => Assert.NotNull(header.Element("Label")));
        var labels = headers.Select(header => header.Element("Label")!.Value).ToList();

        Assert.Contains("FormatsToProcess = 'Compute.format.ps1xml'", File.ReadAllText(manifestPath));
        Assert.Equal(
            "Microsoft.Azure.Azclips.Extensions.Compute.Models.VirtualMachine",
            view.Descendants("TypeName").Single().Value);
        Assert.Equal(["Name", "ResourceGroupName", "Location", "Size", "ProvisioningState"], labels);
    }
}
