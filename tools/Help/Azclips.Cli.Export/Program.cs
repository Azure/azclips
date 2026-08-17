// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azclips.Cli.Export;
using Azclips.Help.Tool;
using Core.Abstractions;
using Core.Cli.Help;
using Core.Runtime;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
var defaultArtifactsRoot = Path.Combine(repoRoot, "artifacts", "Debug");
var defaultOutputFolder = Path.Combine(repoRoot, "artifacts", "cli-export");

var artifactsRoot = GetArg(args, "--artifacts-root") ?? defaultArtifactsRoot;
var outputFolder = GetArg(args, "--output-folder") ?? defaultOutputFolder;
var extensionFilter = GetArg(args, "--extension");

if (!Directory.Exists(artifactsRoot))
{
    Console.Error.WriteLine($"Artifacts root not found: {artifactsRoot}");
    return 1;
}

ExtensionAssemblyLoader.InstallResolver(artifactsRoot);
var extensionDlls = ExtensionAssemblyLoader.EnumerateExtensionAssemblies(artifactsRoot);

if (extensionDlls.Count == 0)
{
    Console.WriteLine($"No extension assemblies found under {artifactsRoot}");
    return 0;
}

var helpFilesByVerb = new Dictionary<string, List<HelpFile>>(StringComparer.OrdinalIgnoreCase);
var seenExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var discoveredExtensions = new List<string>();

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

    if (extensionFilter is not null
        && !extensionName.Equals(extensionFilter, StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }

    IExtensionIndex extensionIndex;
    try
    {
        extensionIndex = assembly.GetExtensionIndex();
    }
    catch
    {
        // Assembly may not expose an extension index (e.g. ExtensionRuntime); skip.
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

if (extensionFilter is not null && !seenExtensions.Contains(extensionFilter))
{
    Console.Error.WriteLine(
        $"Extension '{extensionFilter}' not found. Available: {string.Join(", ", discoveredExtensions.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))}");
    return 1;
}

var latestFolder = Path.Combine(outputFolder, "latest");
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

Console.WriteLine($"Wrote {helpFilesByVerb.Count} verb file(s) to {latestFolder}");
return 0;

static string? GetArg(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

// Strips the leading verb (+ space) from a subgroup CLI path so it can be fed to
// MarkdownCommandHelpProvider.Get(verb, commandKey) — that API expects the segment
// AFTER the extension name / verb.
static string TrimVerbPrefix(string subGroupPath, string verb)
{
    var prefix = verb + " ";
    return subGroupPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        ? subGroupPath[prefix.Length..]
        : subGroupPath;
}
