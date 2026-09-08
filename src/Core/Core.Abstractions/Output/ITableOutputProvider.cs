// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Output;

/// <summary>
/// Implemented by model types that declare a default table view for PowerShell and CLI output.
/// The static members allow core infrastructure (e.g. <see cref="TableOutputXmlGenerator"/>) to
/// introspect column definitions without requiring an object instance, so any module can reuse
/// the same mechanism without duplicating formatting logic.
/// </summary>
public interface ITableOutputProvider
{
    /// <summary>
    /// Fully-qualified CLR type name used in the <c>ViewSelectedBy/TypeName</c> element of
    /// a PowerShell format file. Typically <c>typeof(T).FullName</c>.
    /// </summary>
    static abstract string TableTypeName { get; }

    /// <summary>
    /// Ordered list of column definitions for the default table view.
    /// </summary>
    static abstract IReadOnlyList<TableOutputColumn> GetTableColumns();
}
