// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Extensions;

/// <summary>
/// Read-only <see cref="IExtensionRegistryStore"/> that always yields an empty registry.
/// Use when a dispatcher must operate without file-system state (built-in verbs only).
/// </summary>
public sealed class EmptyExtensionRegistryStore : IExtensionRegistryStore
{
    public ExtensionRegistry Load() => new();

    public void Save(ExtensionRegistry registry)
        => throw new NotSupportedException($"{nameof(EmptyExtensionRegistryStore)} is read-only.");
}
