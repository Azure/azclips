// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;

namespace Core.Cli.Help;

/// <summary>
/// Shared reflection helpers that both the build-time exporter and the runtime help adapter
/// use to read presentation metadata (currently just <c>[ParameterGroup]</c>) off the property
/// backing a bound CLI parameter. Consolidating here keeps the two consumers from drifting.
/// </summary>
internal static class ProcessorParameterReflection
{
    /// <summary>
    /// Resolves the processor property that backs a bound CLI parameter: first by direct
    /// property-name match (case-insensitive), then by falling back to properties whose
    /// <c>[ArgumentInfo(SerializedName = ...)]</c> targets the parameter. Returns null when
    /// no match is found (extension processors don't have to declare properties for every
    /// parameter — synthetic params like the injected <c>profile</c> flag have no backing property).
    /// </summary>
    public static PropertyInfo? FindPropertyForParameter(Type processorType, string parameterName)
    {
        var direct = processorType.GetProperty(parameterName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (direct != null)
        {
            return direct;
        }

        return processorType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(propertyInfo => string.Equals(
                propertyInfo.GetCustomAttribute<ArgumentInfoAttribute>()?.SerializedName,
                parameterName,
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Reads <c>[ParameterGroup]</c> off the property; null when absent or property is null.</summary>
    public static string? GetGroupName(PropertyInfo? property)
        => property?.GetCustomAttribute<ParameterGroupAttribute>()?.Name;

    /// <summary>Reads <c>[PopulatorCommand]</c> off the property; null when absent or property is null.</summary>
    public static string? GetValueSources(PropertyInfo? property)
        => property?.GetCustomAttribute<PopulatorCommandAttribute>()?.Command;
}
