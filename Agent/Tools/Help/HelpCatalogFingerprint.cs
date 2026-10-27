// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Core.Abstractions;
using Core.Cli;
using Core.Cli.Help;
using Core.Configuration.Options;

namespace Core.Agent.Tools.Help;

/// <summary>
/// Computes the header that identifies the installed CLI and extensions; a different header means the help catalog is stale.
/// Invalidates help from the loader's input files, not a second implementation of
/// version selection. Generation still uses IAssemblyLoadingService (including pins).
/// Dependency DLLs and all version directories are included conservatively.
/// </summary>
public static class HelpCatalogFingerprint
{
    public const int CatalogSchemaVersion = 1;

    public static HelpCatalogHeader Read(string azExePath, IConfigurationService configuration, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var executable = CliInstallation.ResolvePath(azExePath);
        var baseDirectory = Path.GetDirectoryName(executable)!;
        var options = configuration.GetConfiguration<AssemblyLoadingOptions>();
        var entries = new List<string> { baseDirectory, options.ExtensionFolderPrefix };
        AddFile(Path.Combine(baseDirectory, "az.dll"), entries);
        AddFile(Path.Combine(baseDirectory, OperatingSystem.IsWindows() ? "az.exe" : "az"), entries);

        var prefixes = ExtensionDiscoveryPaths.PackagePrefixes
            .Append(options.ExtensionFolderPrefix).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        foreach (var root in ExtensionDiscoveryPaths.GetBundledRoots(baseDirectory))
        {
            entries.Add(root);
            if (!Directory.Exists(root)) continue;
            foreach (var package in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
            {
                ct.ThrowIfCancellationRequested();
                if (prefixes.Any(prefix => Path.GetFileName(package).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                    AddPackage(package, entries);
            }
        }

        // An empty root means no installed extensions, as in the CLI loader; Path.GetFullPath("") would throw.
        var installedRoot = string.IsNullOrWhiteSpace(options.InstalledExtensionsRoot)
            ? string.Empty
            : Path.GetFullPath(options.InstalledExtensionsRoot);
        entries.Add(installedRoot);
        if (Directory.Exists(installedRoot))
        {
            foreach (var package in Directory.EnumerateDirectories(installedRoot).Order(StringComparer.Ordinal))
            {
                ct.ThrowIfCancellationRequested();
                entries.Add(package);
                foreach (var version in Directory.EnumerateDirectories(package).Order(StringComparer.Ordinal))
                    AddPackage(version, entries);
            }
        }

        var registry = Path.Combine(installedRoot, HomePaths.RegistryFileName);
        if (File.Exists(registry))
        {
            entries.Add(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(registry))));
        }

        foreach (var helpRoot in ExtensionDiscoveryPaths.GetHelpRoots(baseDirectory))
        {
            entries.Add(helpRoot);
            if (!Directory.Exists(helpRoot)) continue;
            foreach (var file in Directory.EnumerateFiles(helpRoot, "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                ct.ThrowIfCancellationRequested();
                AddFile(file, entries);
            }
        }

        ct.ThrowIfCancellationRequested();
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', entries))));
        return new HelpCatalogHeader(CatalogSchemaVersion, CliInstallation.GetVersion(executable), fingerprint);
    }

    private static void AddPackage(string directory, List<string> entries)
    {
        entries.Add(directory);
        foreach (var file in Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal))
        {
            var extension = Path.GetExtension(file);
            if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".nupkg", StringComparison.OrdinalIgnoreCase))
                AddFile(file, entries);
        }
    }

    private static void AddFile(string path, List<string> entries)
    {
        var info = new FileInfo(path);
        entries.Add(info.Exists
            ? string.Create(CultureInfo.InvariantCulture, $"{path}|{info.Length}|{info.LastWriteTimeUtc.Ticks}")
            : path + "|missing");
    }
}
