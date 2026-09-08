// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Buckets a parameter into a named section on the generated reference page
/// (e.g. <c>"Authentication Arguments"</c>, <c>"Storage Arguments"</c>). Consumed by
/// the docs exporter; runtime <c>--help</c> ignores it today.
/// </summary>
/// <remarks>
/// The value <c>"Global Arguments"</c> is a docs.ms sentinel — the exporter reserves
/// it for the global-flags bucket. Extension authors should not use it.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ParameterGroupAttribute : Attribute
{
    public ParameterGroupAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Group name must be non-empty.", nameof(name));
        }

        Name = name;
    }

    public string Name { get; }
}
