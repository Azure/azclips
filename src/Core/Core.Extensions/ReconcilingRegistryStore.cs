// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Extensions;

/// <summary>
/// Reconciled view of a <see cref="FileExtensionRegistryStore"/> exposed through
/// <see cref="IExtensionRegistryStore"/>: <see cref="Load"/> drops entries whose install payload
/// is missing on disk so the dispatcher never routes to a stale entry.
/// </summary>
public sealed class ReconcilingRegistryStore : IExtensionRegistryStore
{
    private readonly FileExtensionRegistryStore _inner;

    public ReconcilingRegistryStore(FileExtensionRegistryStore inner) => _inner = inner;

    public ExtensionRegistry Load() => _inner.LoadReconciled();

    public void Save(ExtensionRegistry registry) => _inner.Save(registry);
}
