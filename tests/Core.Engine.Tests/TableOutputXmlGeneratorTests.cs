// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;
using Core.Abstractions.Output;

namespace Core.Engine.Tests;

/// <summary>
/// Unit tests for <see cref="TableOutputXmlGenerator"/> — the core utility shared across
/// all modules for generating PowerShell <c>.format.ps1xml</c> content.
/// </summary>
public class TableOutputXmlGeneratorTests
{
    private sealed class SampleModel : ITableOutputProvider
    {
        static string ITableOutputProvider.TableTypeName => "My.Namespace.SampleModel";

        static IReadOnlyList<TableOutputColumn> ITableOutputProvider.GetTableColumns() =>
        [
            new("FirstName", PropertyName: "First"),
            new("Score", ScriptBlock: "$_.Score * 100"),
        ];
    }

    [Fact]
    public void GeneratePsXml_ProducesValidXml()
    {
        var xml = TableOutputXmlGenerator.GeneratePsXml<SampleModel>();
        var doc = XDocument.Parse(xml);
        Assert.NotNull(doc.Root);
    }

    [Fact]
    public void GeneratePsXml_SetsTypeName()
    {
        var xml = TableOutputXmlGenerator.GeneratePsXml<SampleModel>();
        var typeName = XDocument.Parse(xml).Descendants("TypeName").Single().Value;
        Assert.Equal("My.Namespace.SampleModel", typeName);
    }

    [Fact]
    public void GeneratePsXml_EmitsAllColumnLabels()
    {
        var xml = TableOutputXmlGenerator.GeneratePsXml<SampleModel>();
        var labels = XDocument.Parse(xml)
            .Descendants("TableColumnHeader")
            .Select(h => h.Element("Label")!.Value)
            .ToList();
        Assert.Equal(["FirstName", "Score"], labels);
    }

    [Fact]
    public void GeneratePsXml_EmitsPropertyNameForDirectColumn()
    {
        var xml = TableOutputXmlGenerator.GeneratePsXml<SampleModel>();
        var items = XDocument.Parse(xml).Descendants("TableColumnItem").ToList();
        Assert.Equal("First", items[0].Element("PropertyName")?.Value);
        Assert.Null(items[0].Element("ScriptBlock"));
    }

    [Fact]
    public void GeneratePsXml_EmitsScriptBlockForComputedColumn()
    {
        var xml = TableOutputXmlGenerator.GeneratePsXml<SampleModel>();
        var items = XDocument.Parse(xml).Descendants("TableColumnItem").ToList();
        Assert.Equal("$_.Score * 100", items[1].Element("ScriptBlock")?.Value);
        Assert.Null(items[1].Element("PropertyName"));
    }

    [Fact]
    public void GeneratePsXml_ColumnCountMatchesDefinition()
    {
        var xml = TableOutputXmlGenerator.GeneratePsXml<SampleModel>();
        var doc = XDocument.Parse(xml);
        var headerCount = doc.Descendants("TableColumnHeader").Count();
        var itemCount = doc.Descendants("TableColumnItem").Count();
        // SampleModel defines 2 columns.
        Assert.Equal(2, headerCount);
        Assert.Equal(2, itemCount);
    }
}
