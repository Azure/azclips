// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors.Troubleshoot
{
    /// <summary>
    /// `az troubleshoot list` — lists saved failures locally without AI.
    /// </summary>
    public class TroubleshootListProcessor : AgentProcessorBase
    {
        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);

            IReadOnlyList<FailureRecord> records;
            try
            {
                records = await AzclipsServiceProvider.GetRequiredService<IFailureHistoryStore>().ReadRecordsAsync(Token);
            }
            catch (FailureHistoryException ex)
            {
                FailureHistoryDiagnostics.EmitCauseChain(ex, GetService<ILoggingService>());
                throw new AzclipsErrorException(ex.Code, 1, ex.Message, innerException: ex);
            }

            await WriteOutput(TroubleshootEntry.List(records));
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
