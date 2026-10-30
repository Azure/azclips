// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

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
        string outputPath = await DeploymentTemplateFile.SaveAsync(ExtractTemplate(body), Name, Path, Force);
        await WriteOutput(new DeploymentTemplateSaveResult { Path = outputPath });
        return true;
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
