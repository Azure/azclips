// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using IOPath = System.IO.Path;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Saves the template used for a resource-group-scoped deployment to a file on disk. This is the
/// AzClips equivalent of <c>Save-AzResourceGroupDeploymentTemplate</c>.
/// <para>
/// Orchestrator (handwritten/customized) half of the deployment-export command. It is the same
/// single ARM call as <see cref="ExportResourceGroupDeploymentTemplateProcessor"/> — which it
/// inherits, so it accepts every parameter the atomic processor does — plus the local file
/// handling that only the PowerShell surface exposes. Keeping that file I/O here leaves the
/// atomic processor free of client-specific behavior.
/// </para>
/// </summary>
public class SaveResourceGroupDeploymentTemplateProcessor_Orchestrator : ExportResourceGroupDeploymentTemplateProcessor
{
    private const string TemplateExtension = ".json";

    private static readonly JsonSerializerOptions s_indented = new() { WriteIndented = true };

    /// <summary>
    /// Output path for the template file. The PowerShell cmdlet always supplies an absolute path
    /// (resolved against the session's current file-system location), so this never has to
    /// interpret a relative path against the process working directory.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Overwrite the destination file when it already exists.</summary>
    public bool Force { get; set; }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentExportResult? body)
    {
        JsonElement template = ExtractTemplate(body);
        string outputPath = ResolveOutputPath();

        // The extension runtime has no interactive prompt, so an existing file is reported rather
        // than silently overwritten or silently skipped.
        if (File.Exists(outputPath) && !Force)
        {
            throw new InvalidOperationException($"File '{outputPath}' already exists. Use -Force to overwrite it.");
        }

        string? directory = IOPath.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(template, s_indented));
        await WriteOutput(new DeploymentTemplateSaveResult { Path = outputPath });
        return true;
    }

    // Mirrors the legacy path rules: an existing directory receives "<deployment name>.json",
    // anything else is treated as the file itself and gains a ".json" extension when it lacks one.
    private string ResolveOutputPath()
    {
        string basePath = string.IsNullOrWhiteSpace(Path)
            ? IOPath.Combine(Directory.GetCurrentDirectory(), Name)
            : Path;

        if (Directory.Exists(basePath))
        {
            return IOPath.Combine(basePath, Name + TemplateExtension);
        }

        return basePath.EndsWith(TemplateExtension, StringComparison.OrdinalIgnoreCase)
            ? basePath
            : basePath + TemplateExtension;
    }
}

/// <summary>
/// Result of saving an exported deployment template: the full path of the file that was written.
/// </summary>
public class DeploymentTemplateSaveResult
{
    public string Path { get; set; } = string.Empty;

    public override string ToString() => Path;
}
