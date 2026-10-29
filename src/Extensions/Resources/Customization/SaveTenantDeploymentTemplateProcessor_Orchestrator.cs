// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using IOPath = System.IO.Path;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>Save-AzTenantDeploymentTemplate</c>: export the template, then write it to a file, like
/// <see cref="SaveResourceGroupDeploymentTemplateProcessor_Orchestrator"/>.
/// </summary>
public class SaveTenantDeploymentTemplateProcessor_Orchestrator : ExportTemplateAtTenantScopeDeploymentsProcessor
{
    private const string TemplateExtension = ".json";

    private static readonly JsonSerializerOptions s_indented = new() { WriteIndented = true };

    /// <summary>Output path; the cmdlet resolves it against the PowerShell current location.</summary>
    public string Path { get; set; } = string.Empty;

    public bool Force { get; set; }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentExportResult? body)
    {
        JsonElement template = body?.Template
            ?? throw new InvalidOperationException($"The export response for deployment '{DeploymentName}' at tenant scope did not contain a template.");
        string outputPath = ResolveOutputPath();

        // No interactive prompt in the extension runtime, so an existing file needs -Force.
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

    // An existing directory receives "<deployment name>.json"; any other path gains ".json" when it lacks one.
    private string ResolveOutputPath()
    {
        string basePath = string.IsNullOrWhiteSpace(Path)
            ? IOPath.Combine(Directory.GetCurrentDirectory(), DeploymentName)
            : Path;

        if (Directory.Exists(basePath))
        {
            return IOPath.Combine(basePath, DeploymentName + TemplateExtension);
        }

        return basePath.EndsWith(TemplateExtension, StringComparison.OrdinalIgnoreCase)
            ? basePath
            : basePath + TemplateExtension;
    }
}
