// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;
using Core.Cli;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Regression: <c>InstalledExtensionLoader.ExtensionLoadContext</c> must be constructed with
/// the extension's *directory* (not its primary DLL path). Its <c>Load(AssemblyName)</c> override
/// composes <c>Path.Combine(ExtensionDirectory, "...dll")</c>; passing the DLL path itself
/// produced non-existent paths like <c>foo.dll/Other.dll</c>, breaking dependency resolution
/// for any cache-bypassed extension. Pins the contract.
/// </summary>
[Collection("Extension Load Tracker Tests")]
public class ExtensionLoadContextDirectoryTests
{
    [Fact]
    public void ExtensionDirectory_ShouldExposeConstructorArgumentVerbatim()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"azclips-alc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var ctx = new InstalledExtensionLoader.ExtensionLoadContext(tempDir);

            Assert.Equal(tempDir, ctx.ExtensionDirectory);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ExtensionDirectory_ShouldBeUsableAsBaseForSiblingResolution()
    {
        // Behavioural pin: composing Path.Combine(ExtensionDirectory, "Sibling.dll") must produce
        // a path under the directory. If the constructor's parameter were ever reverted to a
        // DLL path, this composition would land at "foo.dll/Sibling.dll" and the Exists check
        // below would fail because the parent isn't a directory.
        var tempDir = Path.Combine(Path.GetTempPath(), $"azclips-alc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var siblingPath = Path.Combine(tempDir, "Sibling.dll");
        File.WriteAllBytes(siblingPath, new byte[] { 0x4D, 0x5A }); // MZ stub; existence check only
        try
        {
            var ctx = new InstalledExtensionLoader.ExtensionLoadContext(tempDir);
            var composed = Path.Combine(ctx.ExtensionDirectory, "Sibling.dll");

            Assert.True(File.Exists(composed),
                $"Expected sibling DLL at '{composed}' but it was not found. " +
                "If this fails, ExtensionLoadContext was constructed with a DLL path instead of a directory.");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}

