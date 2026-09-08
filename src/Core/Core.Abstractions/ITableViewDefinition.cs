// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Provides the column definitions for a PowerShell table-format view.
/// Implement this interface and reference it from <see cref="AzTableViewAttribute"/> to declare
/// how a type should be displayed in a table.
/// </summary>
public interface ITableViewDefinition
{
    /// <summary>
    /// Returns the ordered list of columns for the table view.
    /// </summary>
    IReadOnlyList<TableColumnDefinition> GetColumns();
}
