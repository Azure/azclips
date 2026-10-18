// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Runtime.Loader;
using Core.Abstractions;
using Core.Cli;

namespace Cli.Tests;

public class HostSharedAssemblyPolicyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadContexts_UseHostSharedAssembliesEvenWhenPrivateCopyExists(bool installed)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"azclips-shared-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Assembly[] sharedAssemblies =
            [
                typeof(HostSharedAssemblyPolicy).Assembly,
                typeof(Microsoft.Extensions.DependencyInjection.ServiceCollection).Assembly
            ];
            foreach (var assembly in sharedAssemblies)
            {
                File.Copy(assembly.Location, Path.Combine(directory, Path.GetFileName(assembly.Location)));
            }

            AssemblyLoadContext context = installed
                ? new InstalledExtensionLoader.ExtensionLoadContext(directory)
                : new BundledExtensionLoader.ExtensionLoadContext(typeof(BundledExtensionLoader).Assembly.Location);

            foreach (var assembly in sharedAssemblies)
            {
                Assert.True(HostSharedAssemblyPolicy.IsShared(assembly.GetName().Name));
                Assert.Same(assembly, context.LoadFromAssemblyName(assembly.GetName()));
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
