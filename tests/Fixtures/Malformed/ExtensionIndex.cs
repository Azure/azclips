// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Azclips.Tests.Fixtures.Malformed;

/// <summary>
/// Test fixture: declares <c>[ExtensionVerb]</c> but does NOT implement <see cref="IExtensionIndex"/>.
/// Used to verify <c>LeafReader.ReadContract</c> treats this as a malformed extension (throws)
/// rather than silently returning the same empty contract as an extension with no CLI surface.
/// </summary>
[ExtensionVerb("malformed")]
public sealed class ExtensionIndex
{
}
