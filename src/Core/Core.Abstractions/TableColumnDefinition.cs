// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Defines a single column in a PowerShell table format view.
/// Exactly one of <see cref="PropertyName"/> or <see cref="ScriptBlock"/> is non-null.
/// </summary>
public sealed class TableColumnDefinition
{
    /// <summary>The column header label.</summary>
    public string Label { get; }

    /// <summary>
    /// Name of the object property to display, or <see langword="null"/> when a script block is used.
    /// </summary>
    public string? PropertyName { get; }

    /// <summary>
    /// PowerShell script block expression to evaluate for the cell value, or <see langword="null"/>
    /// when a property name is used.
    /// </summary>
    public string? ScriptBlock { get; }

    private TableColumnDefinition(string label, string? propertyName, string? scriptBlock)
    {
        Label = label;
        PropertyName = propertyName;
        ScriptBlock = scriptBlock;
    }

    /// <summary>Creates a column definition that reads a named property.</summary>
    public static TableColumnDefinition FromProperty(string label, string propertyName)
        => new(label, propertyName, null);

    /// <summary>Creates a column definition that evaluates a PowerShell script block.</summary>
    public static TableColumnDefinition FromScript(string label, string scriptBlock)
        => new(label, null, scriptBlock);
}
