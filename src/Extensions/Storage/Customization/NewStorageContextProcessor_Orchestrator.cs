// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Core.Abstractions;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class NewStorageContextProcessor_Orchestrator : IProcessor
{
    private ProcessorContext processorContext = null!;
    public string StorageAccountName { get; set; } = string.Empty;
    public string? Profile { get; set; }
    public bool UseConnectedAccount { get; set; } = true;
    public bool EnableFileBackupRequestIntent { get; set; }
    public CancellationToken Token => processorContext.Publisher.Token;
    public Action Cancel => processorContext.Publisher.Cancel;

    public IProcessor Init(ProcessorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        processorContext = context;
        return this;
    }

    public async Task ProcessAsync()
    {
        try
        {
            Token.ThrowIfCancellationRequested();
            await processorContext.Publisher.Publish(Events.ProcessorProcessAsyncStart);
            if (!UseConnectedAccount)
                throw new NotSupportedException("Storage contexts currently support connected-account Entra authentication only.");
            StorageContext.ValidateProfile(processorContext.Profile);
            var context = new StorageContext(StorageAccountName, processorContext.Profile!, EnableFileBackupRequestIntent);
            await processorContext.OutputHandler.WriteOutput(context, Token);
        }
        finally
        {
            await processorContext.Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
