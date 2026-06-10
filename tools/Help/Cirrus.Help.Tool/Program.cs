using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Cirrus.Help.Tool;
using Cirrus.Help.Tool.Validation;
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

var structureReport = new MarkdownStructureValidator().Validate(repoRoot);
aggregateReport.Merge(structureReport);

Directory.CreateDirectory(outputRoot);

AssemblyLoadContext.Default.Resolving += (_, assemblyName) =>
{
    var candidate = Directory
        .EnumerateFiles(artifactsRoot, assemblyName.Name + ".dll", SearchOption.AllDirectories)
        .FirstOrDefault();

    return candidate == null ? null : AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(candidate));
};

var extensionDlls = Directory
    .EnumerateFiles(artifactsRoot, "Microsoft.Azure.Cirrus.Extensions.*.dll", SearchOption.AllDirectories)
    .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
    .ToList();

if (extensionDlls.Count == 0)
{
    Console.WriteLine($"No extension assemblies found under {artifactsRoot}");
    return 0;
}

if (scaffoldExtension is not null)
{
    // --scaffold takes a verb (the user-facing CLI noun, e.g. "disk", "vm", "group").
    // Find the extension assembly whose IExtensionIndex.GetVerbs() contains that verb,
    // then scaffold just that verb's slice of the routed command table.
    var scaffoldVerb = scaffoldExtension.ToLowerInvariant();

    IExtensionIndex? scaffoldIndex = null;
    foreach (var dllPath in extensionDlls)
    {
        var candidateAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dllPath));
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

        if (candidateIndex.GetVerbs().Contains(scaffoldVerb, StringComparer.OrdinalIgnoreCase))
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
var validatedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var completenessValidator = new HelpCompletenessValidator();
var indexesByCliGroup = new Dictionary<string, IExtensionIndex>(StringComparer.OrdinalIgnoreCase);

foreach (var dllPath in extensionDlls)
{
    var assemblyPath = Path.GetFullPath(dllPath);
    var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
    var assemblyName = assembly.GetName().Name ?? string.Empty;

    if (!assemblyName.StartsWith("Microsoft.Azure.Cirrus.Extensions.", StringComparison.Ordinal))
    {
        continue;
    }

    var extensionName = assemblyName["Microsoft.Azure.Cirrus.Extensions.".Length..].ToLowerInvariant();
    var version = assembly.GetName().Version?.ToString() ?? "1.0.0";

    IExtensionIndex extensionIndex;
    try
    {
        extensionIndex = assembly.GetExtensionIndex();
    }
    catch (Exception ex)
    {
        // Surface — don't swallow. A broken index would otherwise be invisible
        // in CI and produce an incomplete manifest. Warning (not error) keeps
        // a single corrupt DLL from failing the whole help build, but every
        // failure is reported with its DLL path and the exception message.
        aggregateReport.AddWarning(
            ProblemIds.ExtensionIndexLoadFailed,
            $"Failed to load IExtensionIndex from '{assemblyPath}': {ex.GetType().Name}: {ex.Message}",
            assemblyPath);
        continue;
    }

    if (validatedExtensions.Add(extensionName))
    {
        // Validation is verb-scoped: a multi-verb extension produces one report per
        // verb (e.g. Compute -> vm + disk), each pointing at its own Docs/<verb>/
        // folder. Aggregate them and skip manifest emission if any verb has errors.
        var completenessHasErrors = false;
        foreach (var verb in extensionIndex.GetVerbs())
        {
            var verbReport = completenessValidator.Validate(repoRoot, verb, extensionIndex);
            aggregateReport.Merge(verbReport);
            completenessHasErrors |= verbReport.HasErrors;

            indexesByCliGroup[verb] = extensionIndex;
        }

        // Skip manifest emission for this extension if any verb has completeness errors:
        // a manifest built from an incomplete corpus would be misleading. Other
        // extensions and other validator stages still run so the author sees
        // every problem in one pass.
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

    var extensionOutputDir = Path.Combine(outputRoot, extensionName);
    Directory.CreateDirectory(extensionOutputDir);
    foreach (var manifest in verbManifests)
    {
        // One file per verb so multi-verb extensions don't collide on HelpManifest.json.
        var fileName = verbManifests.Count == 1 ? "HelpManifest.json" : $"HelpManifest.{manifest.Verb}.json";
        WriteJson(Path.Combine(extensionOutputDir, fileName), manifest);
    }
}

var exampleSyntaxReport = new ExampleSyntaxValidator().Validate(repoRoot, indexesByCliGroup);
aggregateReport.Merge(exampleSyntaxReport);


// Build the cmdlet index from Microsoft.Azure.Cirrus.Cmdlets.*.dll (sibling assemblies in
// artifacts/Debug/) and validate every powershell/pwsh fence in help markdown against it.
var cmdletIndex = new CmdletIndex();
var cmdletDlls = Directory
    .EnumerateFiles(artifactsRoot, "Microsoft.Azure.Cirrus.Cmdlets.*.dll", SearchOption.AllDirectories)
    .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase);

foreach (var dllPath in cmdletDlls)
{
    try
    {
        var cmdletAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dllPath));
        cmdletIndex.AddFromAssembly(cmdletAssembly);
    }
    catch (Exception ex)
    {
        // Surface a warning but don't abort the run — PS validation is additive.
        aggregateReport.AddWarning(
            ProblemIds.Tool_CmdletAssemblyLoadFailed,
            $"Could not load cmdlet assembly '{Path.GetFileName(dllPath)}' for PowerShell example validation: {ex.Message}");
    }
}

var psExampleReport = new PowerShellExampleSyntaxValidator().Validate(repoRoot, cmdletIndex);
aggregateReport.Merge(psExampleReport);

var index = ExtensionHelpManifestBuilder.BuildIndex(manifests);
WriteJson(Path.Combine(outputRoot, "HelpIndex.json"), index);

PrintGroupedSummary(aggregateReport);
Console.WriteLine($"Generated {manifests.Count} help manifest(s) and HelpIndex.json in {outputRoot}");

if (aggregateReport.HasErrors)
{
    var errorCount = aggregateReport.Errors.Count();
    Console.Error.WriteLine($"Help validation failed: {errorCount} error(s) collected across all stages. See grouped summary above.");
    return 1;
}

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
