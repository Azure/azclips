// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using System.Xml.Linq;

namespace Cmdlets.Tests;

public class GetVMFormattingTests
{
    private static readonly string[] ExpectedLabels =
        ["Name", "ResourceGroupName", "Location", "Size", "ProvisioningState"];

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
        Assert.Equal(ExpectedLabels, labels);
    }

    [Fact]
    public void VirtualMachine_HasAzTableViewAttribute()
    {
        var attr = typeof(VirtualMachine)
            .GetCustomAttributes(typeof(AzTableViewAttribute), inherit: false)
            .Cast<AzTableViewAttribute>()
            .Single();

        Assert.Equal(typeof(VirtualMachineTableView), attr.TableViewDefinitionType);
    }

    [Fact]
    public void VirtualMachineTableView_ColumnLabelsMatchFormatFile()
    {
        var definition = new VirtualMachineTableView();
        var columns = definition.GetColumns();
        var labels = columns.Select(c => c.Label).ToList();

        Assert.Equal(ExpectedLabels, labels);
    }

    [Fact]
    public void FormatXmlGenerator_ProducesViewMatchingFormatFile()
    {
        var generated = XDocument.Parse(FormatXmlGenerator.Generate(typeof(VirtualMachine)));
        var staticFile = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Compute.format.ps1xml"));

        var generatedLabels = generated.Descendants("TableColumnHeader")
            .Select(h => h.Element("Label")!.Value)
            .ToList();
        var staticLabels = staticFile.Descendants("TableColumnHeader")
            .Select(h => h.Element("Label")!.Value)
            .ToList();

        Assert.Equal(staticLabels, generatedLabels);
        Assert.Equal(
            staticFile.Descendants("TypeName").Single().Value,
            generated.Descendants("TypeName").Single().Value);
        Assert.Equal(
            staticFile.Descendants("View").Single().Element("Name")!.Value,
            generated.Descendants("View").Single().Element("Name")!.Value);
    }
}
