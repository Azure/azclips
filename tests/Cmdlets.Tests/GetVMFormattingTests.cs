// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Cmdlets.Compute;

namespace Cmdlets.Tests;

public class GetVMFormattingTests
{
    [Fact]
    public void VirtualMachineTableView_HasExpectedColumns()
    {
        var view = ComputeTableViews.VirtualMachine;

        Assert.Equal("Microsoft.Azure.Azclips.Extensions.Compute.Models.VirtualMachine", view.TypeName);
        var labels = view.Columns.Select(c => c.Label).ToList();
        Assert.Equal(["Name", "ResourceGroupName", "Location", "Size", "ProvisioningState"], labels);
    }

    [Fact]
    public void TableFormatPs1XmlBuilder_GeneratesValidPs1Xml()
    {
        var ps1xml = TableFormatPs1XmlBuilder.Build([ComputeTableViews.VirtualMachine]);

        var doc = XDocument.Parse(ps1xml);
        var view = doc.Descendants("View").Single();
        var headers = view.Descendants("TableColumnHeader").ToList();
        Assert.All(headers, header => Assert.NotNull(header.Element("Label")));
        var labels = headers.Select(h => h.Element("Label")!.Value).ToList();

        Assert.Equal(
            "Microsoft.Azure.Azclips.Extensions.Compute.Models.VirtualMachine",
            view.Descendants("TypeName").Single().Value);
        Assert.Equal(["Name", "ResourceGroupName", "Location", "Size", "ProvisioningState"], labels);
    }
}
