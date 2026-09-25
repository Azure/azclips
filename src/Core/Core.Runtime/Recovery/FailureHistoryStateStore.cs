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
    [JsonRequired] public ulong Generation { get; init; }
    [JsonRequired] public bool ClearPending { get; init; }

    // Older control files default to unfenced. A completed clear closes this generation;
    // enabling capture must advance the generation before removing the fence.
    public bool OffFenced { get; init; }
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
            if (Files.HasV2StateArtifacts())
                throw new FailureHistoryException("failure_history_control_missing",
                    "Failure-history allocator state is missing. Existing v2 data cannot be reset or replaced without its sequence state.");
            return null;
        }

        await using (stream.ConfigureAwait(false))
        {
            try
            {
                if (stream.Length > 1024)
                    throw CorruptControl();
                var state = await JsonSerializer.DeserializeAsync<FailureHistoryControl>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
                if (state is null || state.Version != 1 ||
                    (state.OffFenced && (state.ClearPending || state.Generation == 0)))
                    throw CorruptControl();
                return state;
            }
            catch (JsonException) { throw CorruptControl(); }
        }
    }

    internal async Task<FailureHistoryControl?> ReadForMutationAsync(CancellationToken cancellationToken)
    {
        try { return await ReadAsync(cancellationToken).ConfigureAwait(false); }
        catch (FailureHistoryException exception) when (exception.Code == "failure_history_control_missing")
        {
            // A first writer may have created its staging file but not replaced control yet.
            // Retry under the existing lock; genuinely missing allocator state still fails.
            await using var writerLock = await Files.AcquireWriterLockAsync(cancellationToken).ConfigureAwait(false);
            return await ReadAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    internal bool IsClearComplete(FailureHistoryControl? state) =>
        state is { ClearPending: false, OffFenced: true } && !Files.HasControlledData();

    internal async Task<FailureHistoryControl> RegisterCaptureUnderLockAsync(
        FailureHistoryControl? state, CancellationToken cancellationToken)
    {
        var registered = state ?? new();
        if (registered.OffFenced)
            registered = registered with { Generation = NextGeneration(registered), OffFenced = false };
        if (state is null || state.OffFenced)
            await WriteAsync(registered, cancellationToken).ConfigureAwait(false);
        return registered;
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

    internal async Task ClearUnderLockAsync(CancellationToken cancellationToken)
    {
        var state = await ReadAsync(cancellationToken).ConfigureAwait(false) ?? new();
        if (!state.ClearPending)
        {
            state = state with { Generation = NextGeneration(state), ClearPending = true, OffFenced = false };
            await WriteAsync(state, cancellationToken).ConfigureAwait(false);
        }

        Files.DeleteControlledData(cancellationToken);
        await WriteAsync(state with { ClearPending = false, OffFenced = true }, cancellationToken).ConfigureAwait(false);
    }

    private static ulong NextGeneration(FailureHistoryControl state)
    {
        if (state.Generation == ulong.MaxValue)
            throw new FailureHistoryException("failure_history_generation_exhausted", "The failure-history generation is exhausted; it cannot wrap.");
        return checked(state.Generation + 1);
    }

    internal static FailureHistoryException PendingClear() =>
        new("failure_history_clear_pending",
            "Failure-history cleanup is incomplete. Retry 'az config set --key agent.recovery --value off' before re-enabling recovery.");

    internal static FailureHistoryException IncompatibleDraft() =>
        new("failure_history_incompatible_draft",
            "Incompatible v1 draft failure history exists. Its bytes are preserved. Explicitly reset with 'az config set --key agent.recovery --value off', then re-enable manual or auto.");

    internal static FailureHistoryException ClearIncomplete(bool recoveryOff, Exception? reason = null) =>
        new("failure_history_clear_incomplete",
            (recoveryOff ? "Recovery is off, but failure-history cleanup is incomplete. " : "Failure-history cleanup is incomplete. ") +
            (reason is FailureHistoryException { Code: "failure_history_control_missing" or "failure_history_control_corrupt" }
                ? "Restore the correct missing/corrupt allocator state before retrying; previously issued IDs cannot be reconstructed safely. "
                : "Retry 'az config set --key agent.recovery --value off'. ") +
            "The writer lock and any remaining allocator state have not been removed.");

    private static FailureHistoryException CorruptControl() =>
        new("failure_history_control_corrupt",
            "Failure-history allocator state is corrupt or unsupported. It cannot be silently reset because doing so could reuse previously issued IDs.");
}
