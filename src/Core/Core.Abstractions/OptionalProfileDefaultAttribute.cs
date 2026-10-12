// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Marks a processor property whose profile-backed default (see
/// <c>IProfileService.GetProfileKeysForDefault</c>) is optional: the active profile's value is still
/// applied when there is one, but argument binding does not fail when neither the caller nor the
/// profile supplies a value. Commands that establish a profile (login) must be able to run before any
/// profile exists and resolve the value themselves.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class OptionalProfileDefaultAttribute : Attribute
{
}
