using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Xml.Linq;
using Core.Abstractions;
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
    .Where(static path => !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
    .ToList();

if (extensionDlls.Count == 0)
{
    Console.WriteLine($"No extension assemblies found under {artifactsRoot}");
    return;
}

var manifests = new List<HelpManifest>();

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

    var manifest = ExtensionHelpManifestBuilder.BuildManifest(extensionIndex, extensionName, version);
    manifests.Add(manifest);

    var extensionOutputDir = Path.Combine(outputRoot, extensionName);
    Directory.CreateDirectory(extensionOutputDir);
    WriteJson(Path.Combine(extensionOutputDir, "HelpManifest.json"), manifest);
}

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

Console.WriteLine($"Generated {manifests.Count} help manifest(s), HelpIndex.json, and {generatedPwshXmlPaths.Count} PowerShell help XML file(s) in {outputRoot}");

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
                new XElement(maml + "title", $"-------------------------- EXAMPLE {i} --------------------------"),
                new XElement(dev + "code", ex.Command),
                new XElement(dev + "remarks", new XElement(maml + "para", ex.Title))));
            i++;
        }

        root.Add(new XElement(command + "command",
            new XElement(command + "details",
                new XElement(command + "name", cmd.PsName),
                new XElement(maml + "description", new XElement(maml + "para", cmd.Description))),
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
