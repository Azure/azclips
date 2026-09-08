// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Xml;

namespace Core.Abstractions;

/// <summary>
/// Generates a PowerShell <c>.format.ps1xml</c> document from types annotated with
/// <see cref="AzTableViewAttribute"/>.
/// </summary>
public static class FormatXmlGenerator
{
    /// <summary>
    /// Generates a <c>.format.ps1xml</c> XML string for <paramref name="type"/>.
    /// </summary>
    /// <param name="type">
    /// A type decorated with <see cref="AzTableViewAttribute"/>. Throws
    /// <see cref="InvalidOperationException"/> when the attribute is absent.
    /// </param>
    /// <returns>A UTF-8 XML string suitable for saving as a <c>.format.ps1xml</c> file.</returns>
    public static string Generate(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var attr = type.GetCustomAttributes(typeof(AzTableViewAttribute), inherit: false)
                       .Cast<AzTableViewAttribute>()
                       .FirstOrDefault()
                   ?? throw new InvalidOperationException(
                       $"Type '{type.FullName}' is not decorated with [{nameof(AzTableViewAttribute)}].");

        var definition = (ITableViewDefinition)(Activator.CreateInstance(attr.TableViewDefinitionType)
                         ?? throw new InvalidOperationException(
                             $"Could not create an instance of '{attr.TableViewDefinitionType.FullName}'."));

        var columns = definition.GetColumns();
        var typeName = type.FullName
                       ?? throw new InvalidOperationException($"Type '{type}' has no FullName.");
        var viewName = type.Name;

        var sb = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            OmitXmlDeclaration = false,
        };

        using var writer = XmlWriter.Create(sb, settings);

        writer.WriteStartDocument();
        writer.WriteStartElement("Configuration");
        writer.WriteStartElement("ViewDefinitions");
        writer.WriteStartElement("View");

        writer.WriteElementString("Name", viewName);

        writer.WriteStartElement("ViewSelectedBy");
        writer.WriteElementString("TypeName", typeName);
        writer.WriteEndElement(); // ViewSelectedBy

        writer.WriteStartElement("TableControl");

        // Headers
        writer.WriteStartElement("TableHeaders");
        foreach (var col in columns)
        {
            writer.WriteStartElement("TableColumnHeader");
            writer.WriteElementString("Label", col.Label);
            writer.WriteEndElement();
        }
        writer.WriteEndElement(); // TableHeaders

        // Row entries
        writer.WriteStartElement("TableRowEntries");
        writer.WriteStartElement("TableRowEntry");
        writer.WriteStartElement("TableColumnItems");
        foreach (var col in columns)
        {
            writer.WriteStartElement("TableColumnItem");
            if (col.PropertyName is not null)
            {
                writer.WriteElementString("PropertyName", col.PropertyName);
            }
            else if (col.ScriptBlock is not null)
            {
                writer.WriteElementString("ScriptBlock", col.ScriptBlock);
            }
            writer.WriteEndElement(); // TableColumnItem
        }
        writer.WriteEndElement(); // TableColumnItems
        writer.WriteEndElement(); // TableRowEntry
        writer.WriteEndElement(); // TableRowEntries

        writer.WriteEndElement(); // TableControl
        writer.WriteEndElement(); // View
        writer.WriteEndElement(); // ViewDefinitions
        writer.WriteEndElement(); // Configuration
        writer.WriteEndDocument();
        writer.Flush();

        return sb.ToString();
    }
}
