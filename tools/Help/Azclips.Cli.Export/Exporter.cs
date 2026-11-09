// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azclips.Help.Tool;
using Azclips.Help.Tool.Validation;
using Core.Abstractions;
using Core.Cli.Help;
using Core.Runtime;

namespace Azclips.Cli.Export;

internal sealed record ExporterOptions(
    string ArtifactsRoot,
    string OutputFolder,
    string? ExtensionFilter);

/// <summary>
/// Main entry point for the CLI docs export pipeline. Loads AzClips extension assemblies
/// under <see cref="ExporterOptions.ArtifactsRoot"/>, builds per-verb <see cref="HelpManifest"/>s,
/// maps them into CLI2Yaml-shaped <see cref="HelpFile"/> records, and writes one JSON per
/// top-level verb to <c>&lt;OutputFolder&gt;/latest/</c>.
/// </summary>
/// <remarks>
/// Extracted from top-level statements in <c>Program.cs</c> so tests
/// (<c>Azclips.Help.Tool.Tests</c>) can invoke the pipeline in-process instead of spawning
/// <c>dotnet run</c>.
/// </remarks>
internal static class Exporter
{
    public static int Run(ExporterOptions options)
    {
        if (!Directory.Exists(options.ArtifactsRoot))
        {
            Console.Error.WriteLine($"Artifacts root not found: {options.ArtifactsRoot}");
            return 1;
        }

        ExtensionAssemblyLoader.InstallResolver(options.ArtifactsRoot);
        var extensionDlls = ExtensionAssemblyLoader.EnumerateExtensionAssemblies(options.ArtifactsRoot);

        if (extensionDlls.Count == 0)
        {
            Console.Error.WriteLine($"[{ProblemIds.Tool_NoExtensionsFound}] No extension assemblies found under '{options.ArtifactsRoot}'. Build the solution and check --artifacts-root.");
            return 1;
        }

        var helpFilesByVerb = new Dictionary<string, List<HelpFile>>(StringComparer.OrdinalIgnoreCase);
        var seenExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var discoveredExtensions = new List<string>();
        var indexesByExtension = new Dictionary<string, IExtensionIndex>(StringComparer.OrdinalIgnoreCase);

        foreach (var dllPath in extensionDlls)
        {
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dllPath));
            var assemblyName = assembly.GetName().Name ?? string.Empty;

            if (!ExtensionAssemblyLoader.TryGetExtensionName(
                    assemblyName,
                    ExtensionAssemblyLoader.KnownExtensionPrefixes,
                    out var extensionName))
            {
                continue;
            }

            if (!seenExtensions.Add(extensionName))
            {
                // Duplicate DLL path for a previously-processed extension.
                continue;
            }
            discoveredExtensions.Add(extensionName);

            IExtensionIndex extensionIndex;
            try
            {
                extensionIndex = assembly.GetExtensionIndex();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[{ProblemIds.ExtensionIndexLoadFailed}] Failed to load IExtensionIndex from '{dllPath}': {ex.Message}");
                return 1;
            }

            indexesByExtension.Add(assemblyName, extensionIndex);
            if (options.ExtensionFilter is not null
                && !extensionName.Equals(options.ExtensionFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var version = assembly.GetName().Version?.ToString() ?? "1.0.0";
            var verbManifests = ExtensionHelpManifestBuilder.BuildManifests(extensionIndex, version);

            foreach (var manifest in verbManifests)
            {
                var verb = manifest.Verb;
                string SubgroupSummary(string subGroupPath)
                {
                    var commandKey = TrimVerbPrefix(subGroupPath, verb);
                    return MarkdownCommandHelpProvider.Get(verb, commandKey).Summary;
                }

                var helpFiles = HelpFileMapper.Map(manifest, SubgroupSummary).ToList();
                helpFilesByVerb[verb] = helpFiles;
            }
        }

        if (options.ExtensionFilter is not null && !seenExtensions.Contains(options.ExtensionFilter))
        {
            Console.Error.WriteLine(
                $"Extension '{options.ExtensionFilter}' not found. Available: {string.Join(", ", discoveredExtensions.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))}");
            return 1;
        }

        var conventionReport = CommandTreeConventionValidation.Validate(indexesByExtension);
        if (conventionReport.HasErrors)
        {
            foreach (var error in conventionReport.Errors)
                Console.Error.WriteLine(error);
            return 1;
        }

        var latestFolder = Path.Combine(options.OutputFolder, "latest");
        Directory.CreateDirectory(latestFolder);

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        foreach (var (verb, helpFiles) in helpFilesByVerb.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var path = Path.Combine(latestFolder, $"{verb}.json");
            var json = JsonSerializer.Serialize(helpFiles, jsonOptions);
            File.WriteAllText(path, json + Environment.NewLine);
        }

        // CLI2Yaml requires a synthesised top-level "az" group at "_root_.json"; without it
        // Transformer.BuildAllSDPGroupMapping throws KeyNotFoundException. Skip when filtering
        // to a single extension since the root is a core-only concept.
        var wroteRoot = false;
        if (options.ExtensionFilter is null)
        {
            var rootPath = Path.Combine(latestFolder, "_root_.json");
            var rootJson = JsonSerializer.Serialize(new[] { HelpFileMapper.CreateRootGroup() }, jsonOptions);
            File.WriteAllText(rootPath, rootJson + Environment.NewLine);
            wroteRoot = true;
        }

        Console.WriteLine($"Wrote {helpFilesByVerb.Count} verb file(s){(wroteRoot ? " + _root_.json" : "")} to {latestFolder}");
        return 0;
    }

    // Strips the leading verb (+ space) from a subgroup CLI path so it can be fed to
    // MarkdownCommandHelpProvider.Get(verb, commandKey) — that API expects the segment
    // AFTER the extension name / verb.
    private static string TrimVerbPrefix(string subGroupPath, string verb)
    {
        var prefix = verb + " ";
        return subGroupPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? subGroupPath[prefix.Length..]
            : subGroupPath;
    }
}
