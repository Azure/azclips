// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions.Recovery;

namespace Core.Runtime.Recovery;

internal sealed record FailureHistoryDocument(ulong NextSequence, IReadOnlyList<StoredFailureRecord> Records)
{
    internal static FailureHistoryDocument Empty { get; } = new(0, []);
}

/// <summary>One history entry. Only the ID, sequence and time are in the clear; an encrypted payload holds the record.</summary>
internal sealed record StoredFailureRecord(string Id, ulong Sequence, DateTimeOffset OccurredAt, string Kind, JsonElement Payload)
{
    internal const string Encrypted = "encrypted";
    internal const string Plain = "plain";
}

internal static class FailureHistoryCodec
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const string AadPrefix = "azclips-failure-history";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static FailureRecord FreezePendingRecord(FailureRecord record)
    {
        ValidateRecord(record, pending: true);
        try { return record with { Arguments = [.. record.Arguments], ErrorDetails = record.ErrorDetails?.Clone() }; }
        catch (ObjectDisposedException) { throw InvalidRecord(); }
    }

    /// <summary>Parses the history file. A file that cannot be parsed is treated as empty history; invalid records are dropped.</summary>
    internal static FailureHistoryDocument Parse(byte[]? content)
    {
        if (content is null)
            return FailureHistoryDocument.Empty;
        try
        {
            var document = JsonSerializer.Deserialize<FailureHistoryDocument>(content, JsonOptions);
            if (document?.Records is null)
                return FailureHistoryDocument.Empty;
            return document with { Records = [.. document.Records.Where(IsValid)] };
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or ArgumentException)
        {
            return FailureHistoryDocument.Empty;
        }
    }

    private static bool IsValid(StoredFailureRecord? record) =>
        record is not null && FailureId.TryParse(record.Id, out _) && record.OccurredAt.Offset == TimeSpan.Zero &&
        ((record.Kind == StoredFailureRecord.Encrypted && record.Payload.ValueKind == JsonValueKind.String) ||
         (record.Kind == StoredFailureRecord.Plain && record.Payload.ValueKind == JsonValueKind.Object));

    internal static byte[] Serialize(FailureHistoryDocument document) => JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);

    internal static StoredFailureRecord Encrypt(FailureRecord record, byte[] key)
    {
        byte[] plaintext;
        try { plaintext = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException) { throw InvalidRecord(); }

        try
        {
            var sealedPayload = new byte[NonceSize + TagSize + plaintext.Length];
            RandomNumberGenerator.Fill(sealedPayload.AsSpan(0, NonceSize));
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(sealedPayload.AsSpan(0, NonceSize), plaintext, sealedPayload.AsSpan(NonceSize + TagSize),
                sealedPayload.AsSpan(NonceSize, TagSize), Aad(record.Id, record.OccurredAtUtc));
            return new(record.Id, record.Sequence, record.OccurredAtUtc, StoredFailureRecord.Encrypted,
                JsonSerializer.SerializeToElement(Convert.ToBase64String(sealedPayload)));
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    internal static StoredFailureRecord ToPlain(FailureRecord record)
    {
        try
        {
            return new(record.Id, record.Sequence, record.OccurredAtUtc, StoredFailureRecord.Plain,
                JsonSerializer.SerializeToElement(record, JsonOptions));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException) { throw InvalidRecord(); }
    }

    /// <summary>Returns the record, or null when it cannot be decrypted or does not match its stored ID and time.</summary>
    internal static FailureRecord? Open(StoredFailureRecord stored, byte[]? key)
    {
        byte[]? plaintext = null;
        try
        {
            if (stored.Kind == StoredFailureRecord.Plain)
                return Validate(stored.Payload.Deserialize<FailureRecord>(JsonOptions), stored);

            if (key is null)
                return null;
            var sealedPayload = Convert.FromBase64String(stored.Payload.GetString()!);
            if (sealedPayload.Length < NonceSize + TagSize)
                return null;
            plaintext = new byte[sealedPayload.Length - NonceSize - TagSize];
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(sealedPayload.AsSpan(0, NonceSize), sealedPayload.AsSpan(NonceSize + TagSize),
                sealedPayload.AsSpan(NonceSize, TagSize), plaintext, Aad(stored.Id, stored.OccurredAt));
            return Validate(JsonSerializer.Deserialize<FailureRecord>(plaintext, JsonOptions), stored);
        }
        catch (Exception exception) when (exception is
            JsonException or FormatException or CryptographicException or NotSupportedException or ArgumentException)
        {
            return null;
        }
        finally
        {
            if (plaintext is not null)
                CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static FailureRecord? Validate(FailureRecord? record, StoredFailureRecord stored)
    {
        if (record is null || record.Id != stored.Id || record.Sequence != stored.Sequence || record.OccurredAtUtc != stored.OccurredAt)
            return null;
        try { ValidateRecord(record, pending: false); }
        catch (FailureHistoryException) { return null; }
        return record;
    }

    private static byte[] Aad(string id, DateTimeOffset occurredAt) =>
        Encoding.UTF8.GetBytes($"{AadPrefix}|{id}|{occurredAt.UtcTicks}");

    private static void ValidateRecord(FailureRecord? record, bool pending)
    {
        if (record is null || record.TerminalSessionId == 0 ||
            record.OccurredAtUtc.Offset != TimeSpan.Zero || record.OccurredAtUtc == default ||
            record.Arguments is null || record.Arguments.Any(argument => argument is null) ||
            record.FailedCommand is null || record.ErrorText is null || record.ExitCode == 0)
            throw InvalidRecord();
        if (pending)
        {
            if (record.Id != string.Empty || record.Sequence != 0)
                throw InvalidRecord();
        }
        else if (!FailureId.TryParse(record.Id, out var id) || id.TerminalSessionId != record.TerminalSessionId)
        {
            throw InvalidRecord();
        }
    }

    private static FailureHistoryException InvalidRecord() =>
        new("failure_history_invalid_record", "The failure record has invalid or missing required fields, or could not be securely serialized.");
}
