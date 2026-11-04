// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Net;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class StartStorageBlobCopyPowerShellProcessor_Orchestrator : BlobWritePowerShellOrchestratorBase
{
    public StartStorageBlobCopyProcessor_Orchestrator? Request { get; set; }

    public StorageBlobPowerShellPlan Prepare(bool force)
    {
        var request = Request ?? throw new ArgumentException("Specify copy settings.");
        var frozen = request.FreezePowerShellRequest();
        if (frozen.SourceContext is null && frozen.SourceBlobObject is null && frozen.SourceContainerObject is null &&
            frozen.SourceFileObject is null && frozen.SourceDirectoryObject is null && frozen.SourceShareObject is null)
        {
            var destination = frozen.InputObject?.Context ?? frozen.ContainerObject?.Context ?? frozen.Context ??
                throw new ArgumentException("Specify a native destination context or resource.");
            Uri? sourceUri = frozen.SourceUri is null ? null : new Uri(frozen.SourceUri, UriKind.Absolute);
            bool files = frozen.SourceShareName is not null || frozen.SourcePath is not null ||
                sourceUri?.IdnHost.EndsWith(".file.core.windows.net", StringComparison.OrdinalIgnoreCase) == true;
            string account = frozen.SourceStorageAccountName ?? (sourceUri is null ? destination.StorageAccountName :
                StorageContext.ValidatePublicUri(new UriBuilder(sourceUri) { Query = "" }.Uri,
                    files ? StorageService.Files : StorageService.Blob));
            frozen.SourceContext = new StorageContext(account, destination.Profile,
                enableFileBackupRequestIntent: files && frozen.EnableSourceFileBackupRequestIntent);
        }
        string target = frozen.ValidatePowerShellCopy();
        var container = frozen.ContainerObject!;
        return new(container, frozen.Blob!, target, "Start blob copy", "Copy", force, child =>
        {
            BlobPowerShellOptions.Target(frozen, child);
            if (child is StartStorageBlobCopyProcessor_Orchestrator copy) frozen.ApplyPowerShellRequest(copy);
        });
    }

    protected override Task ExecuteAsync() => ExecutePlanAsync(Plan ?? throw new ArgumentException("Specify a prepared copy plan."),
        async plan =>
        {
            var child = CreateBlobProcessor<StartStorageBlobCopyProcessor_Orchestrator>(plan.Container, processor => ConfigureWrite(processor, plan));
            child.Overwrite = plan.Force;
            await child.ProcessNativeAsync();
        });
}
