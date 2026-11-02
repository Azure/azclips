// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Text.Json;
using Core.Abstractions.Recovery;
using Core.Runtime.Recovery;

namespace Core.Cli.Recovery;

internal static class FailureHistoryWorker
{
    internal const string Argument = "--failure-history-worker";

    private sealed record SaveRequest(FailureRecord Record, int MaxEntries);
    private sealed record SaveResponse(FailureHistoryEntry? Entry, string? Code = null, string? Message = null, string? Cause = null);

    internal static async Task<FailureHistoryEntry> SaveAsync(
        FailureRecord record, int maxEntries, CancellationToken cancellationToken)
    {
        SaveResponse? response = null;
        var exit = await PipeProcessRunner.RunAsync(
            BuildStartInfo, new SaveRequest(record, maxEntries), cancellationToken,
            async (process, ct) =>
            {
                var discardedDiagnostics = process.StandardError.BaseStream.CopyToAsync(Stream.Null, ct);
                var result = JsonSerializer.DeserializeAsync<SaveResponse>(
                    process.StandardOutput.BaseStream, cancellationToken: ct).AsTask();
                await Task.WhenAll(result, discardedDiagnostics).ConfigureAwait(false);
                response = await result.ConfigureAwait(false);
            }).ConfigureAwait(false);

        if (response?.Code is { } code)
            throw new FailureHistoryException(code, response.Message ?? "Secure failure history is unavailable.",
                response.Cause is { } cause ? new Exception(cause) : null);
        if (exit != 0 || response?.Entry is not { } entry || !FailureId.TryParse(entry.Id, out _))
            throw new FailureHistoryException("capture_unconfirmed", "The history worker did not confirm a saved failure.");
        return entry;
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

    internal static ProcessStartInfo BuildStartInfo(string inputHandle)
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
        return start;
    }
}
