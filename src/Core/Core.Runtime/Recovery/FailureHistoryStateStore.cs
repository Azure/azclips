// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions.Recovery;

namespace Core.Runtime.Recovery;

internal sealed record FailureHistoryControl
{
    [JsonRequired] public int Version { get; init; } = 1;
    [JsonRequired] public ulong NextSequence { get; init; }
}

internal sealed class FailureHistoryStateStore(FailureHistoryFiles files)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    internal FailureHistoryFiles Files { get; } = files;

    internal async Task<FailureHistoryControl?> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FileStream stream;
        try
        {
            stream = new FileStream(Files.ControlPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            if (Files.HasHistoryData())
                throw new FailureHistoryException("failure_history_control_missing",
                    "Failure-history allocator state is missing. Existing history cannot be replaced without its sequence state.");
            return null;
        }

        await using (stream.ConfigureAwait(false))
        {
            try
            {
                if (stream.Length > 1024)
                    throw CorruptControl();
                var state = await JsonSerializer.DeserializeAsync<FailureHistoryControl>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
                if (state is null || state.Version != 1)
                    throw CorruptControl();
                return state;
            }
            catch (JsonException) { throw CorruptControl(); }
        }
    }

    internal Task WriteAsync(FailureHistoryControl state, CancellationToken cancellationToken) =>
        Files.WriteAtomicallyAsync(Files.ControlPath, ".control-",
            (stream, token) => JsonSerializer.SerializeAsync(stream, state, JsonOptions, token), cancellationToken);

    internal async Task<FailureHistoryControl> ReserveSequenceAsync(FailureHistoryControl state, CancellationToken cancellationToken)
    {
        if (state.NextSequence == ulong.MaxValue)
            throw new FailureHistoryException("failure_history_sequence_exhausted", "The failure-history UInt64 sequence is exhausted; it cannot wrap or be reset.");
        var reserved = state with { NextSequence = checked(state.NextSequence + 1) };
        await WriteAsync(reserved, cancellationToken).ConfigureAwait(false);
        return reserved;
    }

    internal static FailureHistoryException ClearIncomplete() =>
        new("failure_history_clear_incomplete",
            "Failure history could not be cleared. Run 'az config set --key agent.recovery.trigger --value off' again.");

    private static FailureHistoryException CorruptControl() =>
        new("failure_history_control_corrupt",
            "Failure-history allocator state is corrupt or unsupported. It cannot be silently reset because doing so could reuse previously issued IDs.");
}
