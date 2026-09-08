// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Describes a PowerShell table format view for a specific .NET type.
/// Use <see cref="TableFormatPs1XmlBuilder"/> to convert instances of this class into
/// <c>.format.ps1xml</c> content that PowerShell can consume.
/// </summary>
public sealed class TableFormatView
{
    /// <summary>The name of the view (used as the <c>&lt;Name&gt;</c> element in ps1xml).</summary>
    public string Name { get; }

    /// <summary>The fully-qualified .NET type name this view applies to.</summary>
    public string TypeName { get; }

    /// <summary>Ordered column definitions for the table.</summary>
    public IReadOnlyList<TableColumnDefinition> Columns { get; }

    /// <param name="name">View name (typically the simple class name).</param>
    /// <param name="typeName">Fully-qualified .NET type name.</param>
    /// <param name="columns">Ordered column definitions.</param>
    public TableFormatView(string name, string typeName, IReadOnlyList<TableColumnDefinition> columns)
    {
        Name = name;
        TypeName = typeName;
        Columns = columns;
    }
}
