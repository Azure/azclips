// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Runtime;
using Core.Runtime.Recovery;
using Core.Telemetry;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors.Troubleshoot
{
    /// <summary>
    /// `az troubleshoot recover` — starts AI recovery for one saved failure.
    /// </summary>
    public class TroubleshootRecoverProcessor : AgentProcessorBase
    {
        [ArgumentInfo(Required = false, Description = @"Recover from this exact saved failure. Omit to select the latest saved failure in this terminal, then the global latest.", SerializedName = @"id")]
        public string? Id { get; set; }

        [ArgumentInfo(Required = false, Description = @"Override agent.recovery.style for this recovery only.", SerializedName = @"style")]
        public string? Style { get; set; }

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            try
            {
                await RunAsync();
            }
            catch (FailureHistoryException ex)
            {
                FailureHistoryDiagnostics.EmitCauseChain(ex, GetService<ILoggingService>());
                throw new AzclipsErrorException(ex.Code, 1, ex.Message, innerException: ex);
            }
            catch (System.Text.Json.JsonException)
            {
                throw new AzclipsErrorException("InvalidConfiguration", 1, "The local configuration is not valid JSON.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or
                InvalidOperationException or ArgumentException or FormatException)
            {
                throw new AzclipsErrorException("TroubleshootFailed", 1, ex.Message, innerException: ex);
            }
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }

        private async Task RunAsync()
        {

            if (Id is not null && !FailureId.TryParse(Id, out _))
                throw AzclipsErrorException.UsageError("--id must be the full failure ID printed by az or az troubleshoot list.");

            var store = AzclipsServiceProvider.GetRequiredService<IFailureHistoryStore>();
            var options = AzclipsServiceProvider.GetRequiredService<IConfigurationService>().GetConfiguration<AgentOptions>();
            var style = Style?.ToLowerInvariant();
            RequireRecovery(options, style,
                Environment.GetEnvironmentVariable(AgentLauncher.AgentModeEnvVar) == "1",
                Console.IsInputRedirected, Console.IsOutputRedirected);

            var selected = TroubleshootSelection.Select(
                await ReadRecordsAsync(store, Id, Token, GetService<ILoggingService>()), Id, TerminalSession.GetParentProcessId, ConsoleSink.Warn);
            var agentExe = AgentLauncher.FindAgentExe()
                ?? throw new AzclipsErrorException("AgentUnavailable", 1, "AI recovery is unavailable: azclips-agent was not found alongside az.");

            var exit = await RunRecoveryAsync(selected, options, agentExe, style, Token);
            if (exit == 130)
                throw new AzclipsErrorException("Cancelled", 130, "AI troubleshooting was cancelled.");
            if (exit != 0)
                throw new AzclipsErrorException("TroubleshootFailed", exit, "AI troubleshooting failed.");
        }

        // A plain selected record (the --id record, or the newest without --id) needs no key,
        // so key storage being unavailable does not block it.
        internal static async Task<IReadOnlyList<FailureRecord>> ReadRecordsAsync(
            IFailureHistoryStore store, string? id, CancellationToken token, ILoggingService? logger = null)
        {
            try
            {
                return await store.ReadRecordsAsync(token);
            }
            catch (FailureHistoryException ex) when (ex.Code == "failure_history_key_unavailable")
            {
                if (await store.ReadPlainAsync(id, token) is { } plain)
                {
                    FailureHistoryDiagnostics.EmitCauseChain(ex, logger);
                    return [plain];
                }
                throw;
            }
        }

        internal static void RequireRecovery(
            AgentOptions options, string? styleOverride, bool inAgentMode, bool inputRedirected, bool outputRedirected)
        {
            if (!options.Recovery.IsEnabled)
                throw new AzclipsErrorException("AgentDisabled", 1,
                    "AI recovery is off. Run az config set --key agent.recovery.trigger --value manual before troubleshooting.");
            if (inAgentMode)
                throw new AzclipsErrorException("RecursiveRecovery", 1, "An agent-internal command cannot start another recovery.");
            if (RecoverySessionSettings.Resolve(options, RecoveryTrigger.Manual, styleOverride).IsInteractive &&
                (inputRedirected || outputRedirected))
                throw AzclipsErrorException.UsageError(
                    "Interactive troubleshooting requires terminal input and output. Use --style oneshot when redirecting input or output.");
        }

        private async Task<int> RunRecoveryAsync(
            FailureRecord failure, AgentOptions options, string agentExe, string? styleOverride, CancellationToken cancellationToken)
        {
            return await RunAttemptAsync(failure, options,
                GetService<ITelemetryService>(), GetService<ICommandTelemetryContext>().Capture(),
                (request, context, token) => AgentProcessRunner.RunAsync(
                    agentExe, request, context, context.InvocationId, token, failure.WorkingDirectory),
                cancellationToken, styleOverride);
        }

        internal static async Task<int> RunAttemptAsync(
            FailureRecord failure,
            AgentOptions options,
            ITelemetryService telemetry,
            TelemetryInvocationContext context,
            Func<RecoveryRequest, TelemetryInvocationContext, CancellationToken, Task<int>> runAgent,
            CancellationToken cancellationToken,
            string? styleOverride = null)
        {
            var session = RecoverySessionSettings.Resolve(options, RecoveryTrigger.Manual, styleOverride);
            var commandTraceId = ValidTraceIdentifier(failure.CommandTraceId, 32);
            var commandSpanId = ValidTraceIdentifier(failure.CommandSpanId, 16);
            if (commandTraceId is not null && commandSpanId is not null)
            {
                context = context with
                {
                    CommandTraceId = commandTraceId,
                    CommandSpanId = commandSpanId,
                    CommandTraceParent = $"00-{commandTraceId}-{commandSpanId}-01",
                    CommandTraceState = null,
                };
            }

            using var recoveryScope = context.TryGetCommandParentContext(out var commandParent)
                ? telemetry.StartActivityWithParent("CLI.Activity.AiRecovery", commandParent)
                : telemetry.StartIndependentRootActivity("CLI.Activity.AiRecovery");
            var recovery = recoveryScope?.Activity;
            context = context.WithRecoveryParent(recovery);
            RecoveryTelemetry.TagRecoveryContext(recovery, context);
            RecoveryTelemetry.RecordConfiguration(recovery, options, session.SessionStyle);
            recovery?.SetTag(TelemetryTags.RecoveryTrigger, "manual");
            var request = new RecoveryRequest
            {
                Trigger = session.Trigger,
                FailedCommand = failure.FailedCommand,
                ErrorText = failure.ErrorText,
                SessionStyle = session.SessionStyle,
                FailureId = failure.Id,
            };

            try
            {
                // Oneshot exits 0 only after it shows a plan or final response; interactive reports its last interaction.
                var exit = await runAgent(request, context, cancellationToken);
                recovery?.SetTag(TelemetryTags.RecoveryExitCode, exit);
                recovery?.SetStatus(exit == 0 ? ActivityStatusCode.Ok :
                    exit == 130 ? ActivityStatusCode.Unset : ActivityStatusCode.Error);
                return exit;
            }
            catch
            {
                recovery?.SetTag(TelemetryTags.RecoveryExitCode, 1);
                recovery?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
        }

        internal static string? ValidTraceIdentifier(string? value, int length) =>
            value is not null && value.Length == length && value.All(char.IsAsciiHexDigit) && value.Any(character => character != '0')
                ? value.ToLowerInvariant()
                : null;
    }
}
