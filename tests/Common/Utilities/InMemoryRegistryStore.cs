// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Extensions;

namespace Tests.Common.Utilities;

/// <summary>
/// Test fake for <see cref="IExtensionRegistryStore"/>: holds an <see cref="ExtensionRegistry"/>
/// in memory and updates it on <see cref="Save"/>. Replaces the three near-identical local
/// stores that previously lived in <c>ExtensionDispatcherTests</c>, <c>ExtensionResolverTests</c>,
/// and <c>MultiNamespaceTests</c>.
/// </summary>
public sealed class InMemoryRegistryStore(ExtensionRegistry registry) : IExtensionRegistryStore
{
    /// <summary>The current in-memory registry. Updated by <see cref="Save"/>.</summary>
    public ExtensionRegistry Saved { get; private set; } = registry;

    public ExtensionRegistry Load() => Saved;

    public void Save(ExtensionRegistry registry) => Saved = registry;
}
