// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Saves the template used for a subscription-scoped deployment to a file on disk. This is the
/// AzClips equivalent of <c>Save-AzDeploymentTemplate</c>.
/// <para>
/// Orchestrator (handwritten/customized) half of the subscription deployment-export command. It is
/// the same single ARM call as <see cref="ExportSubscriptionDeploymentTemplateProcessor"/>, which it
/// inherits, plus the local file handling shared with
/// <see cref="SaveResourceGroupDeploymentTemplateProcessor_Orchestrator"/>.
/// </para>
/// </summary>
public class SaveSubscriptionDeploymentTemplateProcessor_Orchestrator : ExportSubscriptionDeploymentTemplateProcessor
{
    /// <summary>
    /// Output path for the template file. The PowerShell cmdlet always supplies an absolute path
    /// (resolved against the session's current file-system location).
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
