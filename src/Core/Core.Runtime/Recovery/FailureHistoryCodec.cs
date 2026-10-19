// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Core.Abstractions.Recovery;

namespace Core.Runtime.Recovery;

internal static class FailureHistoryCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers =
            {
                static typeInfo =>
                {
                    if (typeInfo.Type == typeof(FailureRecord))
                    {
                        foreach (var name in new[] { "schemaVersion", "id", "terminalSessionId", "sequence", "historyGeneration" })
                            typeInfo.Properties.Single(property => property.Name == name).IsRequired = true;
                    }
                },
            },
        },
    };

    internal static FailureRecord FreezePendingRecord(FailureRecord record)
    {
        ValidateRecord(record, pending: true);
        try { return record with { Arguments = [.. record.Arguments], ErrorDetails = record.ErrorDetails?.Clone() }; }
        catch (ObjectDisposedException) { throw InvalidRecord(); }
    }

    internal static IReadOnlyList<FailureRecord> DecryptRecords(FailureHistorySnapshot snapshot, byte[] key)
    {
        var plaintext = new byte[snapshot.Ciphertext!.Length];
        try
        {
            try
            {
                using var aes = new AesGcm(key, FailureHistorySnapshot.TagSize);
                aes.Decrypt(snapshot.Nonce!, snapshot.Ciphertext, snapshot.Tag!, plaintext, snapshot.AuthenticatedHeader);
            }
            catch (CryptographicException)
            {
                throw new FailureHistoryException("failure_history_authentication_failed",
                    "Failure history could not be authenticated. Its protected key or encrypted data may be missing or damaged.");
            }

            FailureRecord[] records;
            try { records = JsonSerializer.Deserialize<FailureRecord[]>(plaintext, JsonOptions) ?? throw FailureHistorySnapshot.Corrupt(); }
            catch (JsonException) { throw FailureHistorySnapshot.Corrupt(); }
            if (records.Length != snapshot.Entries.Count)
                throw FailureHistorySnapshot.Corrupt();
            for (var index = 0; index < records.Length; index++)
            {
                var record = records[index];
                ValidateRecord(record, pending: false);
                if (record.Id != snapshot.Entries[index].Id ||
                    record.OccurredAtUtc != snapshot.Entries[index].OccurredAtUtc ||
                    record.HistoryGeneration != snapshot.Generation)
                    throw FailureHistorySnapshot.Corrupt();
            }
            return records;
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    internal static async Task WriteAsync(
        FailureHistoryFiles files, IReadOnlyList<FailureRecord> records, ulong generation, byte[] key, CancellationToken cancellationToken)
    {
        byte[] plaintext;
        try { plaintext = JsonSerializer.SerializeToUtf8Bytes(records, JsonOptions); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException) { throw InvalidRecord(); }

        var nonce = RandomNumberGenerator.GetBytes(FailureHistorySnapshot.NonceSize);
        var tag = new byte[FailureHistorySnapshot.TagSize];
        var ciphertext = new byte[plaintext.Length];
        byte[] header;
        try
        {
            header = FailureHistorySnapshot.CreateHeader(records, plaintext.Length, generation);
            using var aes = new AesGcm(key, FailureHistorySnapshot.TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, header);
        }
        catch (CryptographicException) { throw InvalidRecord(); }
        finally { CryptographicOperations.ZeroMemory(plaintext); }

        await files.WriteAtomicallyAsync(files.HistoryPath, ".history-v2-", async (stream, token) =>
        {
            await stream.WriteAsync(header, token).ConfigureAwait(false);
            await stream.WriteAsync(nonce, token).ConfigureAwait(false);
            await stream.WriteAsync(tag, token).ConfigureAwait(false);
            await stream.WriteAsync(ciphertext, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    internal static Task WritePlaintextAsync(FailureHistoryFiles files, FailureRecord record, CancellationToken cancellationToken)
    {
        byte[] content;
        try { content = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException) { throw InvalidRecord(); }
        return files.WriteAtomicallyAsync(files.PlaintextHistoryPath, ".history-plain-",
            (stream, token) => stream.WriteAsync(content, token).AsTask(), cancellationToken, ownerOnly: true);
    }

    internal static async Task<FailureRecord?> ReadPlaintextAsync(FailureHistoryFiles files, CancellationToken cancellationToken)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(files.PlaintextHistoryPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException) { return null; }
        await using (stream.ConfigureAwait(false))
        {
            FailureRecord? record;
            try { record = await JsonSerializer.DeserializeAsync<FailureRecord>(stream, JsonOptions, cancellationToken).ConfigureAwait(false); }
            catch (JsonException) { throw PlaintextCorrupt(); }
            if (record is null)
                throw PlaintextCorrupt();
            try { ValidateRecord(record, pending: false); }
            catch (FailureHistoryException) { throw PlaintextCorrupt(); }
            return record;
        }
    }

    internal static FailureHistoryException PlaintextCorrupt() =>
        new("failure_history_plaintext_corrupt", "The unencrypted latest-failure file is malformed or does not match the history state.");

    private static void ValidateRecord(FailureRecord? record, bool pending)
    {
        if (record is null || record.TerminalSessionId == 0 ||
            record.OccurredAtUtc.Offset != TimeSpan.Zero || record.OccurredAtUtc == default ||
            record.Arguments is null || record.Arguments.Any(argument => argument is null) ||
            record.FailedCommand is null || record.ErrorText is null || record.ExitCode == 0)
            throw InvalidRecord();
        if (record.SchemaVersion != FailureRecord.CurrentSchemaVersion)
            throw FailureHistorySnapshot.UnsupportedSchema();
        if (pending)
        {
            if (record.Id != string.Empty || record.Sequence != 0)
                throw InvalidRecord();
        }
        else if (!FailureId.TryParse(record.Id, out var id) ||
                 id.TerminalSessionId != record.TerminalSessionId || id.Sequence != record.Sequence ||
                 FailureId.Format(record.TerminalSessionId, record.OccurredAtUtc, record.Sequence) != record.Id)
        {
            throw InvalidRecord();
        }
    }

    private static FailureHistoryException InvalidRecord() =>
        new("failure_history_invalid_record", "The failure record has invalid or missing required fields, or could not be securely serialized.");
}
