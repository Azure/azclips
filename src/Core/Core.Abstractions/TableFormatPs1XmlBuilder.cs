// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Core.Abstractions;

/// <summary>
/// Builds <c>.format.ps1xml</c> XML content from a collection of <see cref="TableFormatView"/>
/// definitions. The resulting XML string can be written to a file and loaded by PowerShell via
/// <c>Update-FormatData</c> or <c>FormatsToProcess</c> in a module manifest.
/// </summary>
public static class TableFormatPs1XmlBuilder
{
    /// <summary>
    /// Generates a complete <c>.format.ps1xml</c> document from the supplied views.
    /// </summary>
    /// <param name="views">One or more table format view definitions.</param>
    /// <returns>A UTF-8 XML string suitable for writing to a <c>.format.ps1xml</c> file.</returns>
    public static string Build(IEnumerable<TableFormatView> views)
    {
        var viewElements = views.Select(BuildView);

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("Configuration",
                new XElement("ViewDefinitions",
                    viewElements)));

        return doc.Declaration + Environment.NewLine + doc.ToString();
    }

    private static XElement BuildView(TableFormatView view)
    {
        var headers = view.Columns.Select(c =>
            new XElement("TableColumnHeader",
                new XElement("Label", c.Label)));

        var items = view.Columns.Select(c =>
            c.PropertyName is not null
                ? new XElement("TableColumnItem", new XElement("PropertyName", c.PropertyName))
                : new XElement("TableColumnItem", new XElement("ScriptBlock", c.ScriptBlock)));

        return new XElement("View",
            new XElement("Name", view.Name),
            new XElement("ViewSelectedBy",
                new XElement("TypeName", view.TypeName)),
            new XElement("TableControl",
                new XElement("TableHeaders", headers),
                new XElement("TableRowEntries",
                    new XElement("TableRowEntry",
                        new XElement("TableColumnItems", items)))));
    }
}
