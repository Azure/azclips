// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Marks a class as having a PowerShell table-format view definition supplied by
/// <paramref name="tableViewDefinitionType"/>.  The type must implement
/// <see cref="ITableViewDefinition"/> and have a public parameterless constructor.
/// </summary>
/// <example>
/// <code>
/// [AzTableView(typeof(VirtualMachineTableView))]
/// public class VirtualMachine { ... }
///
/// public sealed class VirtualMachineTableView : ITableViewDefinition
/// {
///     public IReadOnlyList&lt;TableColumnDefinition&gt; GetColumns() => [ ... ];
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AzTableViewAttribute(Type tableViewDefinitionType) : Attribute
{
    /// <summary>
    /// The <see cref="ITableViewDefinition"/> implementation that supplies column definitions.
    /// </summary>
    public Type TableViewDefinitionType { get; } = tableViewDefinitionType;
}
