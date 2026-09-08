// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Output;

/// <summary>
/// Describes a single column in a table output view.
/// Exactly one of <see cref="PropertyName"/> or <see cref="ScriptBlock"/> must be non-null.
/// </summary>
/// <param name="Label">Column header label.</param>
/// <param name="PropertyName">Direct property name to display. Mutually exclusive with <see cref="ScriptBlock"/>.</param>
/// <param name="ScriptBlock">PowerShell script block expression used to derive the cell value. Mutually exclusive with <see cref="PropertyName"/>.</param>
public record TableOutputColumn(string Label, string? PropertyName = null, string? ScriptBlock = null);
