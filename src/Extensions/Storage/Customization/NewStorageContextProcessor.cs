// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class NewStorageContextProcessor : IProcessor
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
            if (Profile is not null && !string.Equals(Profile, processorContext.Profile!.Name, StringComparison.Ordinal))
                throw new ArgumentException("The selected profile does not match the resolved profile.");
            var context = new StorageContext(StorageAccountName, processorContext.Profile!, EnableFileBackupRequestIntent);
            await processorContext.OutputHandler.WriteOutput(context, Token);
        }
        finally
        {
            await processorContext.Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
