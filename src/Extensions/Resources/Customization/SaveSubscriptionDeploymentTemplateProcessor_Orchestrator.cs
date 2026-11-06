// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using IOPath = System.IO.Path;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>Save-AzDeploymentTemplate</c>: saves the template used for a subscription-scoped deployment
/// to a file on disk. It is the same single ARM call as
/// <see cref="ExportTemplateAtSubscriptionScopeDeploymentsProcessor"/> plus the local file handling
/// that only the PowerShell surface exposes.
/// </summary>
public class SaveSubscriptionDeploymentTemplateProcessor_Orchestrator : ExportTemplateAtSubscriptionScopeDeploymentsProcessor
{
    private const string TemplateExtension = ".json";

    private static readonly JsonSerializerOptions s_indented = new() { WriteIndented = true };

    /// <summary>
    /// Output path for the template file. The PowerShell cmdlet always supplies an absolute path
    /// (resolved against the session's current file-system location).
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Overwrite the destination file when it already exists.</summary>
    public bool Force { get; set; }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentExportResult? body)
    {
        JsonElement template = body?.Template
            ?? throw new InvalidOperationException($"The export response for deployment '{DeploymentName}' did not contain a template.");
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
