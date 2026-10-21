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
    /// `az troubleshoot analyze` — starts AI recovery for one saved failure.
    /// </summary>
    public class TroubleshootAnalyzeProcessor : AgentProcessorBase
    {
        [ArgumentInfo(Required = false, Description = @"Analyze this exact saved failure. Omit to select the latest saved failure in this terminal, then the global latest.", SerializedName = @"id")]
        public string? Id { get; set; }

        [ArgumentInfo(Required = false, Description = @"Override agent.recovery.style for this analysis only.", SerializedName = @"style")]
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
                throw new AzclipsErrorException(ex.Code, 1, ex.Message);
            }
            catch (System.Text.Json.JsonException)
            {
                throw new AzclipsErrorException("InvalidConfiguration", 1, "The local configuration is not valid JSON.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or
                InvalidOperationException or ArgumentException or FormatException)
            {
                throw new AzclipsErrorException("TroubleshootFailed", 1, ex.Message);
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
            RequireAnalysis(options, style,
                Environment.GetEnvironmentVariable(AgentLauncher.AgentModeEnvVar) == "1",
                Console.IsInputRedirected, Console.IsOutputRedirected);

            var selected = TroubleshootSelection.Select(
                await store.ReadRecordsAsync(Token), Id, TerminalSession.GetParentProcessId, ConsoleSink.Warn);
            var agentExe = AgentLauncher.FindAgentExe()
                ?? throw new AzclipsErrorException("AgentUnavailable", 1, "AI recovery is unavailable: azclips-agent was not found alongside az.");

            var exit = await RunRecoveryAsync(selected, options, agentExe, style, Token);
            if (exit == 130)
                throw new AzclipsErrorException("Cancelled", 130, "AI troubleshooting was cancelled.");
            if (exit != 0)
                throw new AzclipsErrorException("TroubleshootFailed", exit, "AI troubleshooting failed.");
        }

        internal static void RequireAnalysis(
            AgentOptions options, string? styleOverride, bool inAgentMode, bool inputRedirected, bool outputRedirected)
        {
            if (!options.Recovery.IsEnabled)
                throw new AzclipsErrorException("AgentDisabled", 1,
                    "AI recovery is off. Run az config set --key agent.recovery --value manual before troubleshooting.");
            if (inAgentMode)
                throw new AzclipsErrorException("RecursiveRecovery", 1, "An agent-internal command cannot start another recovery.");
            if (RecoverySessionSettings.Resolve(options, RecoveryTrigger.Manual, styleOverride).IsInteractive &&
                (inputRedirected || outputRedirected))
                throw AzclipsErrorException.UsageError(
                    "Interactive troubleshooting requires terminal input and output. Use --style oneshot when redirecting input or output.");
        }

        private static async Task<int> RunRecoveryAsync(
            FailureRecord failure, AgentOptions options, string agentExe, string? styleOverride, CancellationToken cancellationToken)
        {
            var root = Activity.Current;
            while (root?.Parent is not null)
                root = root.Parent;
            var routing = (root?.GetTagItem(TelemetryTags.TenantId) as string, root?.GetTagItem(TelemetryTags.DataBoundary) as string);
            return await RunAttemptAsync(failure, options,
                AzclipsServiceProvider.GetRequiredService<ITelemetryService>(), routing,
                (request, traceparent, token) => AgentProcessRunner.RunAsync(agentExe, request, traceparent, token),
                cancellationToken, styleOverride);
        }

        internal static async Task<int> RunAttemptAsync(
            FailureRecord failure,
            AgentOptions options,
            ITelemetryService telemetry,
            (string? TenantId, string? Boundary) routing,
            Func<RecoveryRequest, string?, CancellationToken, Task<int>> runAgent,
            CancellationToken cancellationToken,
            string? styleOverride = null)
        {
            var session = RecoverySessionSettings.Resolve(options, RecoveryTrigger.Manual, styleOverride);
            var requestTraceId = Activity.Current?.TraceId.ToHexString();
            var requestSpanId = Activity.Current?.SpanId.ToHexString();

            using var recovery = telemetry.StartRootActivity(name: "CLI.Activity.AiRecovery");
            recovery?.SetTag(TelemetryTags.TenantId, routing.TenantId);
            recovery?.SetTag(TelemetryTags.DataBoundary, routing.Boundary);
            recovery?.SetTag(TelemetryTags.CommandTraceId, ValidTraceIdentifier(failure.CommandTraceId, 32));
            recovery?.SetTag(TelemetryTags.CommandSpanId, ValidTraceIdentifier(failure.CommandSpanId, 16));
            recovery?.SetTag(TelemetryTags.RecoveryRequestTraceId, requestTraceId);
            recovery?.SetTag(TelemetryTags.RecoveryRequestSpanId, requestSpanId);
            recovery?.SetTag(TelemetryTags.RecoveryTrigger, "manual");
            recovery?.SetTag(TelemetryTags.RecoverySessionStyle, session.SessionStyle);
            recovery?.SetTag(TelemetryTags.RecoveryProvider, options.Provider);
            var request = new RecoveryRequest
            {
                Trigger = session.Trigger,
                FailedCommand = failure.FailedCommand,
                ErrorText = failure.ErrorText,
                SessionStyle = session.SessionStyle,
                FailureId = failure.Id,
                CommandTraceId = ValidTraceIdentifier(failure.CommandTraceId, 32),
                CommandSpanId = ValidTraceIdentifier(failure.CommandSpanId, 16),
                RequestTraceId = requestTraceId,
                RequestSpanId = requestSpanId,
                TenantId = routing.TenantId,
                DataBoundary = routing.Boundary,
            };

            try
            {
                // Oneshot exits 0 only after it shows a plan or final response; interactive reports its last interaction.
                var exit = await runAgent(request, recovery?.Id, cancellationToken);
                recovery?.SetTag(TelemetryTags.RecoveryExitCode, exit);
                recovery?.SetStatus(exit == 0 ? ActivityStatusCode.Ok :
                    exit == 130 ? ActivityStatusCode.Unset : ActivityStatusCode.Error);
                return exit;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                recovery?.SetTag(TelemetryTags.RecoveryExitCode, 130);
                recovery?.SetStatus(ActivityStatusCode.Unset);
                throw;
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
