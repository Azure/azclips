using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Azclips.Help.Tool;
using Azclips.Help.Tool.Validation;
using Core.Abstractions;
using Core.Cli.Help;
using Core.Runtime;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
var defaultArtifactsRoot = Path.Combine(repoRoot, "artifacts", "Debug");
var defaultOutputRoot = Path.Combine(repoRoot, "artifacts", "help");

var artifactsRoot = GetArg(args, "--artifacts-root") ?? defaultArtifactsRoot;
var outputRoot = GetArg(args, "--output-root") ?? defaultOutputRoot;
var scaffoldExtension = GetArg(args, "--scaffold");

if (!Directory.Exists(artifactsRoot))
{
    throw new DirectoryNotFoundException($"Artifacts root not found: {artifactsRoot}");
}

var aggregateReport = new ValidationReport();
var knownExtensionPrefixes = ExtensionAssemblyLoader.KnownExtensionPrefixes;
string[] knownCmdletAssemblyPatterns =
[
    "Microsoft.Azure.Azclips.Cmdlets.*.dll",
];

var structureReport = new MarkdownStructureValidator().Validate(repoRoot);
aggregateReport.Merge(structureReport);

var wellFormednessReport = new MarkdigWellFormednessValidator().Validate(repoRoot);
aggregateReport.Merge(wellFormednessReport);

Directory.CreateDirectory(outputRoot);

ExtensionAssemblyLoader.InstallResolver(artifactsRoot);

var extensionDlls = ExtensionAssemblyLoader.EnumerateExtensionAssemblies(artifactsRoot);

if (extensionDlls.Count == 0)
{
    aggregateReport.AddError(ProblemIds.Tool_NoExtensionsFound,
        $"No extension assemblies found under '{artifactsRoot}'. Build the solution and check --artifacts-root.");
    PrintGroupedSummary(aggregateReport);
    EmitGitHubAnnotations(aggregateReport);
    return 1;
}

if (scaffoldExtension is not null)
{
    // --scaffold takes a verb (the user-facing CLI noun, e.g. "disk", "vm", "group").
    // Find the extension assembly whose [ExtensionVerb] declarations contain that verb,
    // then scaffold just that verb's slice of the routed command table.
    var scaffoldVerb = scaffoldExtension.ToLowerInvariant();

    IExtensionIndex? scaffoldIndex = null;
    foreach (var dllPath in extensionDlls)
    {
        Assembly candidateAssembly;
        try
        {
            candidateAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dllPath));
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or FileNotFoundException)
        {
            // Non-extension DLL or mismatched build under artifactsRoot. Skip rather than
            // aborting the entire --scaffold run for an unrelated assembly.
            continue;
        }

        IExtensionIndex candidateIndex;
        try
        {
            candidateIndex = candidateAssembly.GetExtensionIndex();
        }
        catch
        {
            // Assembly may not expose an extension index (e.g. ExtensionRuntime). Skip.
            continue;
        }

        if (ExtensionVerbDeclarations.GetDeclaredVerbs(candidateIndex).Contains(scaffoldVerb, StringComparer.OrdinalIgnoreCase))
        {
            scaffoldIndex = candidateIndex;
            break;
        }
    }
    if (scaffoldIndex is null)
    {
        Console.Error.WriteLine($"--scaffold: no installed extension contributes verb '{scaffoldVerb}'.");
        return 1;
    }

    return Scaffolder.Scaffold(repoRoot, scaffoldVerb, scaffoldIndex);
}

var manifests = new List<HelpManifest>();
var manifestSets = new List<(string ExtensionName, IReadOnlyList<HelpManifest> Manifests)>();
var validatedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var indexesByExtension = new Dictionary<string, IExtensionIndex>(StringComparer.OrdinalIgnoreCase);
var completenessValidator = new HelpCompletenessValidator();
var indexesByCliGroup = new Dictionary<string, IExtensionIndex>(StringComparer.OrdinalIgnoreCase);

foreach (var dllPath in extensionDlls)
{
    var assemblyPath = Path.GetFullPath(dllPath);
    var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
    var assemblyName = assembly.GetName().Name ?? string.Empty;

    if (!ExtensionAssemblyLoader.TryGetExtensionName(assemblyName, knownExtensionPrefixes, out var extensionName))
    {
        continue;
    }
    var version = assembly.GetName().Version?.ToString() ?? "1.0.0";

    IExtensionIndex extensionIndex;
    try
    {
        extensionIndex = assembly.GetExtensionIndex();
    }
    catch (Exception ex)
    {
        // Promoted from warning to error: a broken IExtensionIndex used to silently drop
        // the extension from the help corpus, hiding genuine CI breakage.
        aggregateReport.AddError(
            ProblemIds.ExtensionIndexLoadFailed,
            $"Failed to load IExtensionIndex from '{assemblyPath}': {ex.GetType().Name}: {ex.Message}",
            assemblyPath);
        continue;
    }

    if (validatedExtensions.Add(extensionName))
    {
        indexesByExtension.Add(assemblyName, extensionIndex);
        // Verb-scoped: a multi-verb extension yields one report per verb (Compute → vm + disk).
        var completenessHasErrors = false;
        foreach (var verb in ExtensionVerbDeclarations.GetDeclaredVerbs(extensionIndex))
        {
            var verbReport = completenessValidator.Validate(repoRoot, verb, extensionIndex);
            aggregateReport.Merge(verbReport);
            completenessHasErrors |= verbReport.HasErrors;

            indexesByCliGroup[verb] = extensionIndex;
        }

        // Skip manifest emission when any verb has completeness errors; a manifest from an
        // incomplete corpus would be misleading. Other stages still run so authors see all
        // problems in one pass.
        if (completenessHasErrors)
        {
            continue;
        }
    }
    else
    {
        // Already produced a manifest for this extensionName via an earlier DLL
        // path (e.g. duplicate copies of the same extension assembly in different
        // artifacts subfolders). Avoid emitting duplicate manifests / index entries.
        continue;
    }

    var verbManifests = ExtensionHelpManifestBuilder.BuildManifests(extensionIndex, version);
    manifests.AddRange(verbManifests);
    manifestSets.Add((extensionName, verbManifests));
}

aggregateReport.Merge(new CommandTreeConventionValidator().Validate(indexesByExtension));

var exampleSyntaxReport = new ExampleSyntaxValidator().Validate(repoRoot, indexesByCliGroup);
aggregateReport.Merge(exampleSyntaxReport);

// Convention warning: `[ParameterGroup]` values must end with " Arguments". Walks every processor
// type reachable through the loaded extension indexes; distinct-ed by the validator itself.
var processorTypes = indexesByCliGroup.Values
    .SelectMany(index => index.GetCommandTable(ClientType.CLI).Values.Select(m => m.ProcessorType));
var groupConventionReport = new ParameterGroupConventionValidator().Validate(processorTypes);
aggregateReport.Merge(groupConventionReport);


// Build the cmdlet index from Microsoft Azure cmdlet assemblies (sibling assemblies in
// artifacts/Debug/) and validate every powershell/pwsh fence in help markdown against it.
var cmdletIndex = new CmdletIndex();
var cmdletDlls = ExtensionAssemblyLoader.EnumerateAssembliesByPattern(artifactsRoot, knownCmdletAssemblyPatterns);

foreach (var dllPath in cmdletDlls)
{
    try
    {
        var cmdletAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dllPath));
        cmdletIndex.AddFromAssembly(cmdletAssembly);
    }
    catch (Exception ex)
    {
        // Promoted from warning to error: PS validation needs every cmdlet assembly
        // to load — otherwise PSL1 "Unknown cmdlet" fires falsely on real cmdlets the
        // index never saw. Hard fail rather than mask the gap.
        aggregateReport.AddError(
            ProblemIds.Tool_CmdletAssemblyLoadFailed,
            $"Could not load cmdlet assembly '{Path.GetFileName(dllPath)}' for PowerShell example validation: {ex.Message}");
    }
}

var psExampleReport = new PowerShellExampleSyntaxValidator().Validate(repoRoot, cmdletIndex);
aggregateReport.Merge(psExampleReport);

// PS-side completeness: every cmdlet declared in <Module>.psd1's CmdletsToExport must
// have a corresponding src/PS/<Module>/help/<Verb-Noun>.md. Mirrors the CLI surface's
// HelpCompletenessValidator. Runs after the per-file PSL1..PSL8 rules so authors see
// content errors first when both fire on the same file.
var psCompletenessReport = new PsCompletenessValidator().Validate(repoRoot);
aggregateReport.Merge(psCompletenessReport);

var index = ExtensionHelpManifestBuilder.BuildIndex(manifests);

PrintGroupedSummary(aggregateReport);

if (aggregateReport.HasErrors)
{
    EmitGitHubAnnotations(aggregateReport);
    var errorCount = aggregateReport.Errors.Count();
    Console.Error.WriteLine($"Help validation failed: {errorCount} error(s) collected across all stages. See grouped summary above.");
    return 1;
}

foreach (var (extensionName, verbManifests) in manifestSets)
{
    var extensionOutputDir = Path.Combine(outputRoot, extensionName);
    Directory.CreateDirectory(extensionOutputDir);
    foreach (var manifest in verbManifests)
    {
        // One file per verb so multi-verb extensions don't collide on HelpManifest.json.
        var fileName = verbManifests.Count == 1 ? "HelpManifest.json" : $"HelpManifest.{manifest.Verb}.json";
        WriteJson(Path.Combine(extensionOutputDir, fileName), manifest);
    }
}
WriteJson(Path.Combine(outputRoot, "HelpIndex.json"), index);
Console.WriteLine($"Generated {manifests.Count} help manifest(s) and HelpIndex.json in {outputRoot}");

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

// Renders the final summary: warnings first, then errors grouped by ProblemId, then
// sorted by file path within each group. Issues without a ProblemId fall into an
// "<unclassified>" bucket so nothing is silently dropped. This is the only summary
// the build prints — individual stages no longer fail-fast.
static void PrintGroupedSummary(ValidationReport report)
{
    var warnings = report.Warnings.ToList();
    var errors = report.Errors.ToList();

    if (warnings.Count == 0 && errors.Count == 0)
    {
        Console.WriteLine("Help validation: no issues.");
        return;
    }

    if (warnings.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"Warnings ({warnings.Count}):");
        foreach (var group in warnings
                     .GroupBy(static i => i.ProblemId ?? "<unclassified>")
                     .OrderBy(static g => g.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"  [{group.Key}] ({group.Count()})");
            foreach (var issue in group.OrderBy(static i => i.FilePath ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine($"    {(issue.FilePath is null ? string.Empty : issue.FilePath + ": ")}{issue.Message}");
            }
        }
    }

    if (errors.Count > 0)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine($"Errors ({errors.Count}):");
        foreach (var group in errors
                     .GroupBy(static i => i.ProblemId ?? "<unclassified>")
                     .OrderBy(static g => g.Key, StringComparer.Ordinal))
        {
            Console.Error.WriteLine($"  [{group.Key}] ({group.Count()})");
            foreach (var issue in group.OrderBy(static i => i.FilePath ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"    {(issue.FilePath is null ? string.Empty : issue.FilePath + ": ")}{issue.Message}");
            }
        }
    }
}

static void WriteJson<T>(string path, T value)
{
    var json = JsonSerializer.Serialize(value, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    });

    File.WriteAllText(path, json + Environment.NewLine);
}

// Emits GitHub Actions workflow commands (::error file=…,line=…,title=…::message) so errors
// show as inline annotations on the PR "Files changed" tab instead of just log text.
// See https://docs.github.com/actions/reference/workflow-commands-for-github-actions.
// Guarded by GITHUB_ACTIONS=true so local runs stay quiet.
static void EmitGitHubAnnotations(ValidationReport report)
{
    if (!string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.Ordinal))
    {
        return;
    }

    foreach (var issue in report.Errors)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(issue.FilePath))
        {
            // Normalise to forward slashes; GitHub matches annotation paths against the
            // repo tree, which is always POSIX-style regardless of the runner OS.
            var normalised = issue.FilePath.Replace('\\', '/');
            parts.Add($"file={EscapeAnnotation(normalised, asProperty: true)}");
        }
        if (issue.Line is int line)
        {
            parts.Add($"line={line}");
        }
        if (!string.IsNullOrEmpty(issue.ProblemId))
        {
            parts.Add($"title={EscapeAnnotation(issue.ProblemId, asProperty: true)}");
        }

        var propList = parts.Count == 0 ? string.Empty : " " + string.Join(",", parts);
        Console.WriteLine($"::error{propList}::{EscapeAnnotation(issue.Message)}");
    }
}

// Encodes the reserved chars from the GH workflow-command grammar. Property values are a strict
// superset of message data — they additionally reserve ',' and ':' as parser delimiters.
static string EscapeAnnotation(string value, bool asProperty = false)
{
    var s = value
        .Replace("%", "%25", StringComparison.Ordinal)
        .Replace("\r", "%0D", StringComparison.Ordinal)
        .Replace("\n", "%0A", StringComparison.Ordinal);
    return asProperty
        ? s.Replace(":", "%3A", StringComparison.Ordinal).Replace(",", "%2C", StringComparison.Ordinal)
        : s;
}
