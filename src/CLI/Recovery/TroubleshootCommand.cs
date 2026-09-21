// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Cli.Help;
using Core.Cli.Output;
using Core.Cli.Parser;
using Core.Configuration.Options;
using Core.DependencyInjection;
using Core.Runtime;
using Core.Telemetry;

namespace Core.Cli.Recovery;

internal static class TroubleshootCommand
{
    internal static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var format = OutputFormat.Table;
        var color = false;
        var invokedAi = false;
        try
        {
            var globals = GlobalArgParser.Parse(args);
            var config = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
            var settings = SessionPolicyResolver.Resolve(
                config.GetConfiguration<CoreOptions>(), config.GetConfiguration<ClientsOptions>(), globals);
            ColorPolicy.Initialize(settings.Color);
            color = ColorPolicy.ShouldEmitColor(Console.IsErrorRedirected);
            format = ErrorHelpPolicy.ResolveErrorFormat(
                ErrorHelpPolicy.IsHumanReadableErrorStream(Console.IsErrorRedirected, globals.Output),
                settings.Format, globals.Output);

            if (globals.Help is { } help)
            {
                if (help.Path.Count != 1 ||
                    !help.Path[0].Equals(IntrinsicVerbs.Troubleshoot, StringComparison.OrdinalIgnoreCase))
                    throw AzclipsErrorException.UsageError("Use az troubleshoot --help.");
                var renderer = new RuntimeHelpRenderer(ColorPolicy.ShouldEmitColor(Console.IsOutputRedirected));
                foreach (var line in renderer.RenderCommand(IntrinsicVerbs.CreateHelp(IntrinsicVerbs.Troubleshoot), 100))
                    Console.Out.WriteLine(line);
                return 0;
            }

            var request = TroubleshootRequest.Parse(globals);
            if (globals.Query is not null)
                JmesPathEvaluator.Validate(globals.Query);

            AgentOptions? options = null;
            if (!request.List)
            {
                options = config.GetConfiguration<AgentOptions>();
                RequireAgentEnabled(options, Environment.GetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE") == "1");
            }

            var store = AzclipsServiceProvider.GetRequiredService<IFailureHistoryStore>();
            var records = await store.ReadRecordsAsync(cancellationToken);
            if (request.List)
            {
                var summaries = records.Select(record => new
                {
                    id = record.Id,
                    occurredAtUtc = record.OccurredAtUtc,
                    command = record.CommandName ?? "(unresolved command)",
                    errorCode = record.ErrorCode ?? record.ExceptionType,
                    exitCode = record.ExitCode,
                }).ToArray();
                Console.Out.WriteLine(CliOutputFormatter.FormatValue(
                    summaries, settings.Format, settings.SimpleFormat,
                    ColorPolicy.ShouldEmitColor(Console.IsOutputRedirected), globals.Query));
                return 0;
            }

            var selected = SelectRecord(records, request.Id);
            var agentExe = Program.FindAgentExe()
                ?? throw new AzclipsErrorException("AgentUnavailable", 1, "AI recovery is unavailable: azclips-agent was not found alongside az.");
            invokedAi = true;
            return await RunRecoveryAsync(selected, options!, agentExe, cancellationToken);
        }
        catch (AzclipsErrorException ex)
        {
            Program.EmitStructuredError(ex, format, color, null);
            return ex.ExitCode;
        }
        catch (FailureHistoryException ex)
        {
            Program.EmitStructuredError(new(ex.Code, 1, ex.Message), format, color, null);
            return 1;
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
        catch (System.Text.Json.JsonException)
        {
            Program.EmitStructuredError(new("InvalidConfiguration", 1, "The local configuration is not valid JSON."), format, color, null);
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException or FormatException)
        {
            Program.EmitStructuredError(new("TroubleshootFailed", 1, ex.Message), format, color, null);
            return 1;
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
            if (invokedAi)
                Program.SpawnTelemetryUploader();
        }
    }

    internal static void RequireAgentEnabled(AgentOptions options, bool inAgentMode)
    {
        if (!options.Enabled)
            throw new AzclipsErrorException("AgentDisabled", 1,
                "AI recovery is disabled. Run az config set --key agent.enabled --value true before troubleshooting.");
        if (inAgentMode)
            throw new AzclipsErrorException("RecursiveRecovery", 1, "An agent-internal command cannot start another recovery.");
    }

    internal static FailureRecord SelectRecord(IReadOnlyList<FailureRecord> records, string? id)
    {
        if (id is null)
            return records.FirstOrDefault()
                ?? throw new AzclipsErrorException("NoFailureHistory", 1, "No saved failures are available. Enable agent.enabled, then run the failing command again.");

        return records.FirstOrDefault(record => record.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            ?? throw new AzclipsErrorException("FailureRecordNotFound", 1, "The selected failure record was not found or has been evicted. Use az troubleshoot --list to select a saved failure.");
    }

    private static async Task<int> RunRecoveryAsync(
        FailureRecord failure, AgentOptions options, string agentExe, CancellationToken cancellationToken)
    {
        var telemetry = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        var config = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
        (string? TenantId, string? Boundary) routing = (null, null);
        if (config.GetConfiguration<TelemetryOptions>().Enabled)
        {
            routing = await TelemetryRoutingResolver.ResolveAsync(
                AzclipsServiceProvider.GetRequiredService<IProfileService>(),
                AzclipsServiceProvider.GetRequiredService<IAuthService>(),
                profileNameOverride: null, cancellationToken);
        }

        return await RunAttemptAsync(failure, options, telemetry, routing,
            (request, traceparent, token) => AgentProcessRunner.RunManualAsync(agentExe, request, traceparent, token),
            cancellationToken);
    }

    internal static async Task<int> RunAttemptAsync(
        FailureRecord failure,
        AgentOptions options,
        ITelemetryService telemetry,
        (string? TenantId, string? Boundary) routing,
        Func<RecoveryRequest, string?, CancellationToken, Task<int>> runAgent,
        CancellationToken cancellationToken)
    {
        string? requestTraceId;
        string? requestSpanId;
        using (var activity = telemetry.StartRootActivity())
        {
            activity?.SetTag(TelemetryTags.Command, IntrinsicVerbs.Troubleshoot);
            activity?.SetTag(TelemetryTags.TenantId, routing.TenantId);
            activity?.SetTag(TelemetryTags.DataBoundary, routing.Boundary);
            activity?.SetStatus(ActivityStatusCode.Ok);
            requestTraceId = activity?.TraceId.ToHexString();
            requestSpanId = activity?.SpanId.ToHexString();
        }

        using var recovery = telemetry.StartRootActivity(name: "CLI.Activity.AiRecovery");
        recovery?.SetTag(TelemetryTags.TenantId, routing.TenantId);
        recovery?.SetTag(TelemetryTags.DataBoundary, routing.Boundary);
        recovery?.SetTag(TelemetryTags.CommandTraceId, ValidTraceIdentifier(failure.CommandTraceId, 32));
        recovery?.SetTag(TelemetryTags.CommandSpanId, ValidTraceIdentifier(failure.CommandSpanId, 16));
        recovery?.SetTag(TelemetryTags.RecoveryRequestTraceId, requestTraceId);
        recovery?.SetTag(TelemetryTags.RecoveryRequestSpanId, requestSpanId);
        recovery?.SetTag(TelemetryTags.RecoveryTrigger, "manual");
        recovery?.SetTag(TelemetryTags.RecoverySessionStyle, "oneshot");
        recovery?.SetTag(TelemetryTags.RecoveryProvider, options.Provider);
        var request = new RecoveryRequest
        {
            Failure = failure,
            RequestTraceId = requestTraceId,
            RequestSpanId = requestSpanId,
            TenantId = routing.TenantId,
            DataBoundary = routing.Boundary,
        };

        try
        {
            var exit = await runAgent(request, recovery?.Id, cancellationToken);
            recovery?.SetTag(TelemetryTags.RecoveryExitCode, exit);
            recovery?.SetStatus(exit == 0 ? ActivityStatusCode.Ok :
                exit == 130 ? ActivityStatusCode.Unset : ActivityStatusCode.Error);
            return exit;
        }
        catch
        {
            recovery?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
    }

    internal static string? ValidTraceIdentifier(string? value, int length) =>
        value is not null && value.Length == length && value.All(char.IsAsciiHexDigit) && value.Any(character => character != '0')
            ? value.ToLowerInvariant()
            : null;
}
