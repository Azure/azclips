// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;

namespace Azclips.Help.Tool.Validation;

/// <summary>
/// Warns when a <c>[ParameterGroup]</c> value doesn't follow the naming convention
/// (must end with <c>" Arguments"</c>). Verified against Azure CLI: every group header in the
/// observed corpus uses this shape (<c>Authentication Arguments</c>, <c>Storage Arguments</c>,
/// <c>Managed Service Identity Arguments</c>, etc.). A typo produces a silently mis-headered
/// section — no rendering failure, but out of convention — so this fires as a warning rather
/// than an error, consistent with the "grouping is opt-in" policy established in PR #711.
/// </summary>
public sealed class ParameterGroupConventionValidator
{
    private const string ExpectedSuffix = " Arguments";

    public ValidationReport Validate(IEnumerable<Type> processorTypes)
    {
        var report = new ValidationReport();
        var seen = new HashSet<(string TypeName, string PropertyName, string GroupName)>();

        foreach (var type in processorTypes.Distinct())
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attribute = property.GetCustomAttribute<ParameterGroupAttribute>();
                if (attribute is null)
                {
                    continue;
                }

                var groupName = attribute.Name;
                if (groupName.EndsWith(ExpectedSuffix, StringComparison.Ordinal))
                {
                    continue;
                }

                // Inherited properties surface once per subtype that inherits them; dedup so a
                // single non-conforming annotation on a base class doesn't spam the report.
                if (!seen.Add((property.DeclaringType?.FullName ?? type.FullName ?? type.Name, property.Name, groupName)))
                {
                    continue;
                }

                var owningType = property.DeclaringType?.Name ?? type.Name;
                var suggestion = groupName + ExpectedSuffix;
                report.AddWarning(
                    ProblemIds.GRP_NonConventionalGroupName,
                    $"[ParameterGroup(\"{groupName}\")] on '{owningType}.{property.Name}' does not follow the convention (should end in \"{ExpectedSuffix}\", e.g. \"{suggestion}\").");
            }
        }

        return report;
    }
}
