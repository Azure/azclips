// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Extensions;

namespace Core.Extensions.Tests;

public sealed class ReconcilingRegistryStoreTests
{
    [Fact]
    public void Load_DropsEntry_WhenPayloadMissing_AndKeeps_WhenPresent()
    {
        var root = Directory.CreateTempSubdirectory("recon-").FullName;
        try
        {
            var registryPath = Path.Combine(root, "registry.json");
            var fileStore = new FileExtensionRegistryStore(registryPath);

            var registry = new ExtensionRegistry();
            registry.Extensions["Pkg.A"] = new ExtensionRegistryEntry
            {
                Verb = "a",
                Version = "1.0.0",
                Contributions = { new Contribution { Verb = "a", Path = "" } },
            };
            fileStore.Save(registry);

            var store = new ReconcilingRegistryStore(new FileExtensionRegistryStore(registryPath));

            // Reconciliation is now a no-op: payload presence does not affect returned entries.
            Assert.Single(store.Load().Extensions);

            // Create payload dir <root>/Pkg.A/1.0.0/ -> still a single entry.
            Directory.CreateDirectory(Path.Combine(root, "Pkg.A", "1.0.0"));
            Assert.Single(store.Load().Extensions);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
