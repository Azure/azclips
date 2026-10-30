// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using IOPath = System.IO.Path;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Writes an exported deployment template to disk with the file rules shared by
/// <c>Save-AzResourceGroupDeploymentTemplate</c> and <c>Save-AzDeploymentTemplate</c>.
/// </summary>
internal static class DeploymentTemplateFile
{
    private const string TemplateExtension = ".json";

    private static readonly JsonSerializerOptions s_indented = new() { WriteIndented = true };

    /// <summary>
    /// Writes <paramref name="template"/> to the file chosen from <paramref name="path"/> and returns its path.
    /// </summary>
    /// <param name="path">
    /// Absolute output path (the PowerShell cmdlets resolve it against the session's current
    /// file-system location); empty means the process working directory.
    /// </param>
    public static async Task<string> SaveAsync(JsonElement template, string deploymentName, string path, bool force)
    {
        string outputPath = ResolveOutputPath(deploymentName, path);

        // The extension runtime has no interactive prompt, so an existing file is reported rather
        // than silently overwritten or silently skipped.
        if (File.Exists(outputPath) && !force)
        {
            throw new InvalidOperationException($"File '{outputPath}' already exists. Use -Force to overwrite it.");
        }

        string? directory = IOPath.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(template, s_indented));
        return outputPath;
    }

    // Mirrors the legacy path rules: an existing directory receives "<deployment name>.json",
    // anything else is treated as the file itself and gains a ".json" extension when it lacks one.
    private static string ResolveOutputPath(string deploymentName, string path)
    {
        string basePath = string.IsNullOrWhiteSpace(path)
            ? IOPath.Combine(Directory.GetCurrentDirectory(), deploymentName)
            : path;

        if (Directory.Exists(basePath))
        {
            return IOPath.Combine(basePath, deploymentName + TemplateExtension);
        }

        return basePath.EndsWith(TemplateExtension, StringComparison.OrdinalIgnoreCase)
            ? basePath
            : basePath + TemplateExtension;
    }
}
