// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Describes a single column in a PowerShell table-format view.
/// Exactly one of <see cref="PropertyName"/> or <see cref="ScriptBlock"/> must be set.
/// </summary>
public sealed record TableColumnDefinition(string Label)
{
    /// <summary>
    /// Name of the property to display in the column. Use when the column maps directly to an object property.
    /// </summary>
    public string? PropertyName { get; init; }

    /// <summary>
    /// PowerShell script block expression to evaluate for the column value. Use for computed columns.
    /// </summary>
    public string? ScriptBlock { get; init; }

    /// <summary>
    /// Creates a column that displays a direct object property.
    /// </summary>
    public static TableColumnDefinition FromProperty(string label, string? propertyName = null) =>
        new(label) { PropertyName = propertyName ?? label };

    /// <summary>
    /// Creates a column whose value is produced by a PowerShell script block expression.
    /// </summary>
    public static TableColumnDefinition FromScript(string label, string scriptBlock) =>
        new(label) { ScriptBlock = scriptBlock };
}
