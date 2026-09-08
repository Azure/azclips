// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Xml;

namespace Core.Abstractions.Output;

/// <summary>
/// Generates a PowerShell <c>.format.ps1xml</c> document from any type that implements
/// <see cref="ITableOutputProvider"/>. Centralising this logic in core means every module
/// can produce consistent format files without duplicating XML serialisation code.
/// </summary>
public static class TableOutputXmlGenerator
{
    /// <summary>
    /// Generates a complete <c>.format.ps1xml</c> XML document for <typeparamref name="T"/>.
    /// </summary>
    public static string GeneratePsXml<T>() where T : ITableOutputProvider
    {
        var typeName = T.TableTypeName;
        var columns = T.GetTableColumns();

        var sb = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            OmitXmlDeclaration = false,
        };

        using (var writer = XmlWriter.Create(sb, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("Configuration");

            writer.WriteStartElement("ViewDefinitions");
            writer.WriteStartElement("View");

            // View name — use the simple type name for readability.
            var simpleName = typeName.Contains('.') ? typeName[(typeName.LastIndexOf('.') + 1)..] : typeName;
            writer.WriteElementString("Name", simpleName);

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

            // Rows
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
        } // Dispose flushes all buffered content to sb.

        return sb.ToString();
    }
}
