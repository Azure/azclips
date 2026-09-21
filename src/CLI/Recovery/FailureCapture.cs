// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Cli.Help;
using Core.Cli.Parser;

namespace Core.Cli.Recovery;

internal sealed record FailureCaptureResult(FailureHistoryEntry? Entry = null, bool ShowHint = false, string? Warning = null)
{
    internal object? Recovery => ShowHint && Entry is not null
        ? new { id = Entry.Id, command = $"az troubleshoot --id {Entry.Id}" }
        : null;
}

internal static class FailureCapture
{
    internal static bool ShouldCapture(string[] args, Exception error, bool inAgentMode) =>
        !inAgentMode &&
        !GlobalArgParser.IsCommandInvocation(args, IntrinsicVerbs.Troubleshoot) &&
        (args.Length == 0 || args[0] is not ("--upload-telemetry" or "--completions" or "--completion-candidates" or "--completion-values" or "--print-probe"));

    internal static async Task<FailureCaptureResult> SaveAsync(
        string[] args,
        string failedCommand,
        Exception error,
        int exitCode,
        string? commandName,
        Activity? activity,
        Func<AgentOptions> getOptions,
        Func<IFailureHistoryStore> getStore,
        bool inAgentMode,
        CancellationToken cancellationToken = default)
    {
        if (exitCode == 0 || cancellationToken.IsCancellationRequested || !ShouldCapture(args, error, inAgentMode))
            return new();

        try
        {
            var options = getOptions();
            if (!options.Enabled)
                return new();
            if (options.AutoRecovery is null || options.FailureHistory is null || options.FailureHistory.MaxEntries < 1)
                throw new ArgumentException("Agent recovery configuration is invalid; failureHistory.maxEntries must be positive.");

            var structured = error as AzclipsErrorException;
            var record = new FailureRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                OccurredAtUtc = DateTimeOffset.UtcNow,
                Arguments = args.ToArray(),
                FailedCommand = failedCommand,
                ErrorText = error.Message,
                ExitCode = exitCode,
                CommandName = commandName,
                ErrorCode = structured?.Code,
                ExceptionType = error.GetType().FullName,
                ErrorDetails = structured is null ? null : JsonSerializer.SerializeToElement(structured.Details),
                HttpStatusCode = error is HttpRequestException { StatusCode: { } status } ? (int)status : null,
                WorkingDirectory = Directory.GetCurrentDirectory(),
                ProfileName = Program.TryGetProfileOverride(args),
                ClientVersion = typeof(Program).Assembly.GetName().Version?.ToString(),
                CommandTraceId = activity?.TraceId.ToHexString(),
                CommandSpanId = activity?.SpanId.ToHexString(),
            };
            var entry = await getStore().SaveAsync(record, options.FailureHistory.MaxEntries, cancellationToken);
            return new(entry, !options.AutoRecovery.Enabled);
        }
        catch (FailureHistoryException ex)
        {
            return new(Warning: $"The failure could not be saved securely ({ex.Code}). {ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            // Do not echo configuration values or the raw diagnostic a second time.
            return new(Warning: "The failure could not be saved securely. Check the local history directory and Agent configuration.");
        }
    }
}
