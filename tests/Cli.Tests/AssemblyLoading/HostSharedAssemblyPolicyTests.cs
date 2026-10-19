// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
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

            var extensionAssembly = typeof(Microsoft.Azure.Azclips.Extensions.Accounts.ExtensionIndex).Assembly;
            var extensionPath = Path.Combine(directory, Path.GetFileName(extensionAssembly.Location));
            File.Copy(extensionAssembly.Location, extensionPath);
            // Declare the private assets explicitly so this test fails if the sharing guard is removed.
            const string target = ".NETCoreApp,Version=v10.0";
            var assets = new Dictionary<string, object>();
            var libraries = new Dictionary<string, object>();
            foreach (var assembly in sharedAssemblies.Append(extensionAssembly))
            {
                var identity = assembly.GetName();
                var id = $"{identity.Name}/{identity.Version}";
                assets[id] = new
                {
                    runtime = new Dictionary<string, object>
                    {
                        [Path.GetFileName(assembly.Location)] = new { assemblyVersion = identity.Version!.ToString() }
                    }
                };
                libraries[id] = new { type = "project", serviceable = false, sha512 = "" };
            }
            File.WriteAllText(Path.ChangeExtension(extensionPath, ".deps.json"), JsonSerializer.Serialize(new
            {
                runtimeTarget = new { name = target },
                targets = new Dictionary<string, object> { [target] = assets },
                libraries
            }));
            var resolver = new AssemblyDependencyResolver(extensionPath);
            foreach (var assembly in sharedAssemblies)
            {
                Assert.Equal(Path.Combine(directory, Path.GetFileName(assembly.Location)),
                    resolver.ResolveAssemblyToPath(assembly.GetName()));
            }

            AssemblyLoadContext context = installed
                ? new InstalledExtensionLoader.ExtensionLoadContext(directory)
                : new BundledExtensionLoader.ExtensionLoadContext(extensionPath);
            var loadedExtension = context.LoadFromAssemblyPath(extensionPath);
            Assert.Same(context, AssemblyLoadContext.GetLoadContext(loadedExtension));
            var index = Activator.CreateInstance(
                loadedExtension.GetType("Microsoft.Azure.Azclips.Extensions.Accounts.ExtensionIndex", throwOnError: true)!);
            Assert.IsAssignableFrom<IExtensionIndex>(index);

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
