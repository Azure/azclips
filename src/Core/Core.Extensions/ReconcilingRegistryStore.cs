// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Extensions;

/// <summary>
/// DEPRECATED in \u00a75.2: No longer needed. DiscoverySession now owns the installed state;
/// reconciliation is implicit. Kept as a passthrough for back-compat; new code should use
/// FileExtensionRegistryStore directly.
/// </summary>
[Obsolete("Registry reconciliation is no longer performed. Use FileExtensionRegistryStore.Load() directly, or consult DiscoverySession for authoritative installed state. Will be removed in \u00a75.3.", error: false)]
public sealed class ReconcilingRegistryStore : IExtensionRegistryStore
{
    private readonly FileExtensionRegistryStore _inner;

    public ReconcilingRegistryStore(FileExtensionRegistryStore inner) => _inner = inner;

    public ExtensionRegistry Load() => _inner.Load();

    public void Save(ExtensionRegistry registry) => _inner.Save(registry);
}
