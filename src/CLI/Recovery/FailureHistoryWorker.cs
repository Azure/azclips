// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Runtime.Recovery;

namespace Core.Cli.Recovery;

internal static class FailureHistoryWorker
{
    internal const string Argument = "--failure-history-worker";

    private sealed record SaveRequest(FailureRecord Record, int MaxEntries);
    private sealed record SaveResponse(FailureHistoryEntry? Entry, string? Code = null, string? Message = null, string? Cause = null);

    // Child diagnostics kept for --debug; the rest of the stream is still drained so the child never blocks.
    private const int MaxDiagnosticChars = 64 * 1024;

    /// <param name="debugLogger">Set only when the parent logs at debug level: the child then logs at debug
    /// level too, and its stderr is written to this logger.</param>
    internal static async Task<FailureHistoryEntry> SaveAsync(
        FailureRecord record, int maxEntries, CancellationToken cancellationToken,
        ILoggingService? debugLogger = null, Func<string, ProcessStartInfo>? createStartInfo = null)
    {
        SaveResponse? response = null;
        var diagnostics = new StringBuilder();
        int exit;
        try
        {
            exit = await PipeProcessRunner.RunAsync(
                createStartInfo ?? (handle => BuildStartInfo(handle, debug: debugLogger is not null)),
                new SaveRequest(record, maxEntries), cancellationToken,
                async (process, ct) =>
                {
                    var stderr = ReadDiagnosticsAsync(
                        process.StandardError.BaseStream, process.StandardError.CurrentEncoding, diagnostics, ct);
                    var result = JsonSerializer.DeserializeAsync<SaveResponse>(
                        process.StandardOutput.BaseStream, cancellationToken: ct).AsTask();
                    await Task.WhenAll(result, stderr).ConfigureAwait(false);
                    response = await result.ConfigureAwait(false);
                }).ConfigureAwait(false);
        }
        finally
        {
            if (debugLogger is not null && diagnostics.Length > 0)
                debugLogger.Debug($"history worker stderr: {diagnostics}");
        }

        if (response?.Code is { } code)
            throw new FailureHistoryException(code, response.Message ?? "Secure failure history is unavailable.",
                response.Cause is { } cause ? new Exception(cause) : null);
        if (exit != 0 || response?.Entry is not { } entry || !FailureId.TryParse(entry.Id, out _))
            throw new FailureHistoryException("capture_unconfirmed", "The history worker did not confirm a saved failure.");
        return entry;
    }

    // Reads bytes, not chars: a byte read returns whatever is available, so text read before
    // cancellation is never held back in a partially filled char request.
    private static async Task ReadDiagnosticsAsync(
        Stream stderr, Encoding encoding, StringBuilder diagnostics, CancellationToken cancellationToken)
    {
        var decoder = encoding.GetDecoder();
        var bytes = new byte[4096];
        var chars = new char[encoding.GetMaxCharCount(bytes.Length)];
        try
        {
            int read;
            while ((read = await stderr.ReadAsync(bytes, cancellationToken).ConfigureAwait(false)) > 0)
            {
                var decoded = decoder.GetChars(bytes, 0, read, chars, 0, flush: false);
                diagnostics.Append(chars, 0, Math.Min(decoded, MaxDiagnosticChars - diagnostics.Length));
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
        {
            // Keep what was read; the caller reports the failure that stopped the child.
        }
    }

    internal static async Task<int?> TryHandleAsync(string[] args, Func<IFailureHistoryStore> getStore)
    {
        if (args.Length == 0 || args[0] != Argument)
            return null;

        var response = await SaveFromPipeAsync(args, getStore).ConfigureAwait(false);
        await JsonSerializer.SerializeAsync(Console.OpenStandardOutput(), response).ConfigureAwait(false);
        return response.Entry is null ? 1 : 0;
    }

    private static async Task<SaveResponse> SaveFromPipeAsync(string[] args, Func<IFailureHistoryStore> getStore)
    {
        try
        {
            if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]))
                throw new ArgumentException("The history worker requires an input pipe.");
            using var input = new AnonymousPipeClientStream(PipeDirection.In, args[1]);
            var request = await JsonSerializer.DeserializeAsync<SaveRequest>(input).ConfigureAwait(false);
            if (request?.Record is null || request.MaxEntries < 1)
                throw new InvalidDataException("The history worker received an incomplete request.");
            var entry = await getStore().SaveAsync(request.Record, request.MaxEntries).ConfigureAwait(false);
            return new(entry);
        }
        catch (FailureHistoryException ex)
        {
            return new(null, ex.Code, ex.Message, ex.ToString());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or
                                      ArgumentException or NotSupportedException)
        {
            return new(null, "capture_failed", "The failure could not be saved securely. Check the local history directory and recovery configuration.", ex.ToString());
        }
    }

    internal static ProcessStartInfo BuildStartInfo(string inputHandle, bool debug = false)
    {
        var currentProcess = Environment.ProcessPath
            ?? throw new InvalidOperationException("The current CLI executable could not be located.");
        var isCliEntry = Assembly.GetEntryAssembly() == typeof(Program).Assembly;
        var start = new ProcessStartInfo
        {
            FileName = isCliEntry ? currentProcess : Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (!isCliEntry || Path.GetFileNameWithoutExtension(currentProcess).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(Program).Assembly.Location);
        start.ArgumentList.Add(Argument);
        start.ArgumentList.Add(inputHandle);
        start.Environment[AgentLauncher.AgentModeEnvVar] = "1";
        if (debug)
            start.Environment["AZCLIPS_LOG_LEVEL"] = "debug";
        return start;
    }
}
