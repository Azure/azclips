// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;
using Core.Abstractions.Output;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

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

    /// <summary>
    /// Verifies that the static Compute.format.ps1xml file is consistent with the column
    /// definitions declared via the core <see cref="ITableOutputProvider"/> implementation on
    /// <see cref="VirtualMachine"/>. If VirtualMachine's column definitions change, this test
    /// will fail until the ps1xml is regenerated to match.
    /// </summary>
    [Fact]
    public void VirtualMachineView_MatchesCoreTableOutputProvider()
    {
        var formatPath = Path.Combine(AppContext.BaseDirectory, "Compute.format.ps1xml");
        var fileContent = File.ReadAllText(formatPath);

        // Generate expected XML from the core ITableOutputProvider implementation.
        var generated = TableOutputXmlGenerator.GeneratePsXml<VirtualMachine>();

        // Compare column labels: the file and the generator must agree on both count and order.
        var fileLabels = XDocument.Parse(fileContent)
            .Descendants("TableColumnHeader")
            .Select(h => h.Element("Label")?.Value)
            .ToList();
        var generatedLabels = XDocument.Parse(generated)
            .Descendants("TableColumnHeader")
            .Select(h => h.Element("Label")?.Value)
            .ToList();

        Assert.Equal(generatedLabels, fileLabels);

        // The type name must also match.
        var fileType = XDocument.Parse(fileContent).Descendants("TypeName").Single().Value;
        var generatedType = XDocument.Parse(generated).Descendants("TypeName").Single().Value;
        Assert.Equal(generatedType, fileType);
    }
}
