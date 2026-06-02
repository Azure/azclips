using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Xml.Linq;
using Cirrus.Help.Tool.Validation;
using Core.Abstractions;
using Core.Cli.Help;
using Core.Runtime;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
var defaultArtifactsRoot = Path.Combine(repoRoot, "artifacts", "Debug");
var defaultOutputRoot = Path.Combine(repoRoot, "artifacts", "help");

var artifactsRoot = GetArg(args, "--artifacts-root") ?? defaultArtifactsRoot;
var outputRoot = GetArg(args, "--output-root") ?? defaultOutputRoot;
var generatePwshXml = !HasArg(args, "--skip-pwsh-xml");

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

var manifests = new List<HelpManifest>();
var validatedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var completenessValidator = new HelpCompletenessValidator();
var indexesByCliGroup = new Dictionary<string, IExtensionIndex>(StringComparer.OrdinalIgnoreCase);

// Maps the first CLI token (e.g. "group") to the extension name that owns it.
// Mirrors src/CLI/Parser/CommandGroupIndex.cs — keep in sync until that mapping
// is exposed on IExtensionIndex.
var cliGroupByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["resources"] = "group",
    ["login"] = "login",
    ["extension"] = "extension",
    ["config"] = "config",
    ["compute"] = "vm",
};

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
    catch
    {
        continue;
    }

    if (validatedExtensions.Add(extensionName))
    {
        var completenessReport = completenessValidator.Validate(repoRoot, extensionName, extensionIndex);
        aggregateReport.Merge(completenessReport);

        if (cliGroupByExtension.TryGetValue(extensionName, out var cliGroup))
        {
            indexesByCliGroup[cliGroup] = extensionIndex;
        }

        // Skip manifest emission for this extension if it has completeness errors:
        // a manifest built from an incomplete corpus would be misleading. Other
        // extensions and other validator stages still run so the author sees
        // every problem in one pass.
        if (completenessReport.HasErrors)
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

    var manifest = ExtensionHelpManifestBuilder.BuildManifest(
        extensionIndex,
        extensionName,
        version,
        cliGroupByExtension.TryGetValue(extensionName, out var cliGroupForManifest) ? cliGroupForManifest : null);
    manifests.Add(manifest);

    var extensionOutputDir = Path.Combine(outputRoot, extensionName);
    Directory.CreateDirectory(extensionOutputDir);
    WriteJson(Path.Combine(extensionOutputDir, "HelpManifest.json"), manifest);
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

var generatedPwshXmlPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
if (generatePwshXml)
{
    var powershellOutputDir = Path.Combine(outputRoot, "powershell");
    Directory.CreateDirectory(powershellOutputDir);

    foreach (var manifest in manifests)
    {
        var cmdletCommands = manifest.Commands
            .Where(static c => !string.IsNullOrWhiteSpace(c.PsName))
            .ToList();

        if (cmdletCommands.Count == 0)
        {
            continue;
        }

        var moduleName = $"Microsoft.Azure.Cirrus.Cmdlets.{ToPascalCase(manifest.Extension)}";
        var xmlPath = Path.Combine(powershellOutputDir, moduleName + ".dll-Help.xml");

        WritePowerShellHelpXml(xmlPath, cmdletCommands, manifest.Extension);
        generatedPwshXmlPaths.Add(xmlPath);
    }
}

PrintGroupedSummary(aggregateReport);
Console.WriteLine($"Generated {manifests.Count} help manifest(s), HelpIndex.json, and {generatedPwshXmlPaths.Count} PowerShell help XML file(s) in {outputRoot}");

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

static bool HasArg(string[] args, string name)
{
    return args.Any(arg => arg.Equals(name, StringComparison.OrdinalIgnoreCase));
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

static void WritePowerShellHelpXml(string path, IList<HelpCommand> commands, string extension)
{
    XNamespace maml = "http://schemas.microsoft.com/maml/2004/10";
    XNamespace command = "http://schemas.microsoft.com/maml/dev/command/2004/10";
    XNamespace dev = "http://schemas.microsoft.com/maml/dev/2004/10";

    var root = new XElement("helpItems",
        new XAttribute(XNamespace.Xmlns + "maml", maml),
        new XAttribute(XNamespace.Xmlns + "command", command),
        new XAttribute(XNamespace.Xmlns + "dev", dev),
        new XAttribute("schema", "maml"));

    foreach (var cmd in commands)
    {
        var syntaxItem = new XElement(command + "syntaxItem",
            new XElement(maml + "name", cmd.PsName));

        foreach (var p in cmd.Parameters)
        {
            syntaxItem.Add(new XElement(command + "parameter",
                new XAttribute("required", p.Required.ToString().ToLowerInvariant()),
                new XAttribute("position", "named"),
                new XAttribute("pipelineInput", p.SupportsPipeline.ToString().ToLowerInvariant()),
                new XAttribute("globbing", p.SupportsWildcards.ToString().ToLowerInvariant()),
                new XElement(maml + "name", p.PsName),
                new XElement(command + "parameterValue", new XAttribute("required", "true"), ToPowerShellTypeName(p.Type)),
                new XElement(maml + "description", new XElement(maml + "para", p.Description))));
        }

        var examples = new XElement(command + "examples");
        var psExamples = cmd.Examples.PowerShell.Count > 0 ? cmd.Examples.PowerShell : cmd.Examples.Cli;
        var i = 1;
        foreach (var ex in psExamples)
        {
            examples.Add(new XElement(command + "example",
                new XElement(maml + "title", $"--- Example {i}: {ex.Title} ---"),
                new XElement(dev + "code", ex.Command),
                new XElement(dev + "remarks", new XElement(maml + "para", string.Empty))));
            i++;
        }

        root.Add(new XElement(command + "command",
            new XElement(command + "details",
                new XElement(command + "name", cmd.PsName),
                // command:details/maml:description is shown as SYNOPSIS — use the short Summary.
                new XElement(maml + "description", new XElement(maml + "para", cmd.Summary))),
            // Top-level maml:description is shown as DESCRIPTION — use the full Description.
            new XElement(maml + "description", new XElement(maml + "para", cmd.Description)),
            new XElement(command + "syntax", syntaxItem),
            examples,
            new XElement(command + "parameters",
                cmd.Parameters.Select(p => new XElement(command + "parameter",
                    new XAttribute("required", p.Required.ToString().ToLowerInvariant()),
                    new XAttribute("position", "named"),
                    new XAttribute("pipelineInput", p.SupportsPipeline.ToString().ToLowerInvariant()),
                    new XAttribute("globbing", p.SupportsWildcards.ToString().ToLowerInvariant()),
                    new XElement(maml + "name", p.PsName),
                    new XElement(command + "parameterValue", new XAttribute("required", "true"), ToPowerShellTypeName(p.Type)),
                    new XElement(maml + "description", new XElement(maml + "para", p.Description))))),
            new XElement(maml + "alertSet", new XElement(maml + "alert", new XElement(maml + "para", $"Generated help for {extension}."))),
            new XElement(maml + "relatedLinks")));
    }

    var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
    doc.Save(path);
}

static string ToPowerShellTypeName(string typeName)
{
    return typeName.ToLowerInvariant() switch
    {
        "string" => "String",
        "bool" => "SwitchParameter",
        "int" => "Int32",
        "long" => "Int64",
        "double" => "Double",
        "float" => "Single",
        _ => typeName,
    };
}

static string ToPascalCase(string value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    if (value.Length == 1)
    {
        return value.ToUpperInvariant();
    }

    return char.ToUpperInvariant(value[0]) + value[1..];
}
