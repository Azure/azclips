// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

/// <summary>
/// Validates that a role assignment name is a GUID at parameter-binding time — i.e. before
/// <c>BeginProcessing</c>/<c>ProcessRecord</c> and therefore before <c>ShouldProcess</c>. Without
/// this, a <c>-WhatIf</c> run would short-circuit inside <c>ShouldProcess</c> and report a successful
/// what-if for a name that a real run is guaranteed to reject in the orchestrator, so the two would
/// disagree. The orchestrator still performs the same check for the CLI surface and as defense in depth.
/// Uses <see cref="Guid.TryParse(string, out Guid)"/> (not a stricter regex) so the accepted set matches
/// the orchestrator exactly.
/// <para>
/// Set <see cref="AllowEmpty"/> for an optional name that is auto-generated when blank (e.g.
/// <c>New-AzRoleAssignment -Name</c>): a null/blank value is accepted (the orchestrator generates one),
/// and only a non-blank value that is not a GUID is rejected. Leave it unset (the default) when the name
/// is required (e.g. <c>Remove-AzRoleAssignment -Name</c>), so a blank value is also rejected.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class ValidateRoleAssignmentNameAttribute : ValidateArgumentsAttribute
{
    /// <summary>When true, a null/blank name is accepted (it will be auto-generated); only a non-blank non-GUID is rejected.</summary>
    public bool AllowEmpty { get; set; }

    public static bool IsValid(object? arguments, bool allowEmpty = false)
    {
        if (arguments is not null && arguments is not string)
        {
            return false;
        }

        string? value = arguments as string;
        if (string.IsNullOrWhiteSpace(value))
        {
            return allowEmpty;
        }

        return Guid.TryParse(value.Trim(), out _);
    }

    protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
    {
        if (!IsValid(arguments, AllowEmpty))
        {
            throw new ValidationMetadataException($"The role assignment name '{arguments}' must be a GUID.");
        }
    }
}
