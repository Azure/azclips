// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Xml.Linq;
using Tests.Common.Utilities;

namespace Cli.Tests;

public class ExtensionPublishAssetsTests
{
    [Fact]
    public async Task Publish_ExcludesHostCoreAssetsButPreservesPrivateDependencies()
    {
        var repository = RepoLocator.GetRepoRoot();
        var configuration = typeof(ExtensionPublishAssetsTests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var temporary = Path.Combine(Path.GetTempPath(), $"azclips-publish-assets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            foreach (var name in new[] { "Accounts", "Config", "Extension", "ManagedServiceIdentity" })
            {
                var assemblyName = $"Microsoft.Azure.Azclips.Extensions.{name}";
                var output = Path.Combine(temporary, name);
                var start = new ProcessStartInfo("dotnet")
                {
                    WorkingDirectory = repository,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                foreach (var argument in new[]
                {
                    "publish", $"src/Extensions/{name}/Extensions.{name}.csproj",
                    "--configuration", configuration, "--no-build", "--no-restore", "--output", output
                })
                {
                    start.ArgumentList.Add(argument);
                }
                using var process = Process.Start(start)!;
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    process.Kill(entireProcessTree: true);
                    throw;
                }
                Assert.True(process.ExitCode == 0, await stdout + await stderr);
                Assert.True(File.Exists(Path.Combine(output, $"{assemblyName}.dll")));
                Assert.Empty(Directory.GetFiles(output, "Core.*.dll", SearchOption.AllDirectories));
                Assert.Empty(Directory.GetFiles(output, "Microsoft.Extensions.DependencyInjection*.dll", SearchOption.AllDirectories));
                if (name == "Extension")
                {
                    Assert.True(File.Exists(Path.Combine(output, "NuGet.Protocol.dll")));
                    Assert.True(File.Exists(Path.Combine(output, "NuGet.Resolver.dll")));
                    Assert.True(File.Exists(Path.Combine(output, "System.Security.Cryptography.Pkcs.dll")));
                }
                else
                {
                    Assert.Single(Directory.GetFiles(output, "*.dll", SearchOption.AllDirectories));
                }

                var artifactDirectory = Path.Combine(repository, "artifacts", configuration, assemblyName);
                using var package = ZipFile.OpenRead(Assert.Single(Directory.GetFiles(artifactDirectory, "*.nupkg")));
                using var nuspec = Assert.Single(package.Entries, entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open();
                var dependencies = XDocument.Load(nuspec).Descendants()
                    .Where(element => element.Name.LocalName == "dependency")
                    .Select(element => element.Attribute("id")!.Value).ToArray();
                Assert.DoesNotContain("Core.DependencyInjection", dependencies);
                Assert.Contains("Core.Abstractions", dependencies);
                if (name == "Config")
                {
                    Assert.Contains("Core.Configuration", dependencies);
                }
                if (name == "Extension")
                {
                    Assert.Contains("Core.Extensions", dependencies);
                    Assert.Contains("NuGet.Protocol", dependencies);
                    Assert.Contains("NuGet.Resolver", dependencies);
                }
                if (name == "ManagedServiceIdentity")
                {
                    Assert.Contains("Microsoft.Azure.Azclips.ExtensionRuntime", dependencies);
                }
            }
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }
}
