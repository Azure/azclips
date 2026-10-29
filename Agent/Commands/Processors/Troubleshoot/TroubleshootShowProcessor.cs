// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Runtime;
using Core.Runtime.Recovery;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors.Troubleshoot
{
    /// <summary>
    /// `az troubleshoot show` — shows one saved failure locally, including the complete command and error.
    /// </summary>
    public class TroubleshootShowProcessor : AgentProcessorBase
    {
        [ArgumentInfo(Required = true, Description = @"Show this exact saved failure locally, including the complete command and error detail.", SerializedName = @"id")]
        public string Id { get; set; } = string.Empty;

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);

            if (!FailureId.TryParse(Id, out _))
                throw AzclipsErrorException.UsageError("--id must be the full failure ID printed by az or az troubleshoot list.");

            IReadOnlyList<FailureRecord> records;
            try
            {
                records = await AzclipsServiceProvider.GetRequiredService<IFailureHistoryStore>().ReadRecordsAsync(Token);
            }
            catch (FailureHistoryException ex)
            {
                throw new AzclipsErrorException(ex.Code, 1, ex.Message, innerException: ex);
            }

            var selected = TroubleshootSelection.Select(records, Id, TerminalSession.GetParentProcessId, ConsoleSink.Warn);
            await WriteOutput(TroubleshootEntry.Detail(selected));
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
