// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers.Binary;
using System.Text;
using Core.Abstractions.Recovery;

namespace Core.Runtime.Recovery;

internal sealed class FailureHistorySnapshot
{
    // v2: magic[8], version:int32, count:int32, ciphertextLength:int64, generation:uint64,
    // indexLength:int32, index entries (UTF-8 length:uint16, ID, UTC ticks:int64),
    // nonce[12], tag[16], ciphertext. The whole header/index is authenticated AES-GCM AAD.
    internal const int HeaderSize = 36;
    internal const int NonceSize = 12;
    internal const int TagSize = 16;
    private const int EnvelopeVersion = 2;
    private static ReadOnlySpan<byte> Magic => "AZCLFHST"u8;

    private FailureHistorySnapshot(byte[] authenticatedHeader, ulong generation, IReadOnlyList<FailureHistoryEntry> entries)
    {
        AuthenticatedHeader = authenticatedHeader;
        Generation = generation;
        Entries = entries;
    }

    internal byte[] AuthenticatedHeader { get; }
    internal ulong Generation { get; }
    internal IReadOnlyList<FailureHistoryEntry> Entries { get; }
    internal byte[]? Nonce { get; private set; }
    internal byte[]? Tag { get; private set; }
    internal byte[]? Ciphertext { get; private set; }

    internal static byte[] CreateHeader(IReadOnlyList<FailureRecord> records, int ciphertextLength, ulong generation)
    {
        var indexLength = records.Sum(record => checked(2 + Encoding.UTF8.GetByteCount(record.Id) + 8));
        var header = new byte[checked(HeaderSize + indexLength)];
        Magic.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8), EnvelopeVersion);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12), records.Count);
        BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(16), ciphertextLength);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(24), generation);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(32), indexLength);
        var offset = HeaderSize;
        foreach (var record in records)
        {
            var length = Encoding.UTF8.GetByteCount(record.Id);
            BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(offset), checked((ushort)length));
            Encoding.UTF8.GetBytes(record.Id, header.AsSpan(offset + 2, length));
            BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(offset + 2 + length), record.OccurredAtUtc.UtcTicks);
            offset += 2 + length + 8;
        }
        return header;
    }

    internal static async Task<FailureHistorySnapshot> ReadAsync(
        FileStream stream, bool includeCiphertext, CancellationToken cancellationToken)
    {
        try
        {
            var fixedHeader = new byte[HeaderSize];
            await stream.ReadExactlyAsync(fixedHeader, cancellationToken).ConfigureAwait(false);
            if (!fixedHeader.AsSpan(0, Magic.Length).SequenceEqual(Magic))
                throw Corrupt();
            if (BinaryPrimitives.ReadInt32LittleEndian(fixedHeader.AsSpan(8)) != EnvelopeVersion)
                throw UnsupportedSchema();

            var count = BinaryPrimitives.ReadInt32LittleEndian(fixedHeader.AsSpan(12));
            var ciphertextLength = BinaryPrimitives.ReadInt64LittleEndian(fixedHeader.AsSpan(16));
            var generation = BinaryPrimitives.ReadUInt64LittleEndian(fixedHeader.AsSpan(24));
            var indexLength = BinaryPrimitives.ReadInt32LittleEndian(fixedHeader.AsSpan(32));
            var headerLength = HeaderSize + (long)indexLength;
            if (count <= 0 || indexLength <= 0 || count > indexLength / 31 || headerLength > Array.MaxLength ||
                ciphertextLength <= 0 || ciphertextLength > Array.MaxLength ||
                stream.Length != headerLength + NonceSize + TagSize + ciphertextLength)
                throw Corrupt();

            var header = new byte[(int)headerLength];
            fixedHeader.CopyTo(header, 0);
            await stream.ReadExactlyAsync(header.AsMemory(HeaderSize), cancellationToken).ConfigureAwait(false);
            var snapshot = new FailureHistorySnapshot(header, generation, ReadEntries(header, count));
            if (includeCiphertext)
            {
                snapshot.Nonce = new byte[NonceSize];
                snapshot.Tag = new byte[TagSize];
                snapshot.Ciphertext = new byte[(int)ciphertextLength];
                await stream.ReadExactlyAsync(snapshot.Nonce, cancellationToken).ConfigureAwait(false);
                await stream.ReadExactlyAsync(snapshot.Tag, cancellationToken).ConfigureAwait(false);
                await stream.ReadExactlyAsync(snapshot.Ciphertext, cancellationToken).ConfigureAwait(false);
            }
            return snapshot;
        }
        catch (EndOfStreamException) { throw Corrupt(); }
    }

    private static FailureHistoryEntry[] ReadEntries(byte[] header, int count)
    {
        var entries = new FailureHistoryEntry[count];
        var offset = HeaderSize;
        ulong? previousSequence = null;
        for (var index = 0; index < count; index++)
        {
            if (header.Length - offset < 2)
                throw Corrupt();
            var length = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(offset));
            if (length is < 21 or > 46 || header.Length - offset < 2 + length + 8)
                throw Corrupt();
            var id = Encoding.UTF8.GetString(header, offset + 2, length);
            var ticks = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(offset + 2 + length));
            if (!FailureId.TryParse(id, out var parsed) || ticks <= 0 || ticks > DateTimeOffset.MaxValue.UtcTicks ||
                (previousSequence.HasValue && parsed.Sequence >= previousSequence.Value))
                throw Corrupt();
            var occurredAt = new DateTimeOffset(ticks, TimeSpan.Zero);
            if (FailureId.Format(parsed.TerminalSessionId, occurredAt, parsed.Sequence) != id)
                throw Corrupt();
            entries[index] = new FailureHistoryEntry(id, occurredAt);
            previousSequence = parsed.Sequence;
            offset += 2 + length + 8;
        }
        if (offset != header.Length)
            throw Corrupt();
        return entries;
    }

    internal static FailureHistoryException Corrupt() =>
        new("failure_history_corrupt", "The encrypted failure-history snapshot is malformed or incomplete.");

    internal static FailureHistoryException UnsupportedSchema() =>
        new("failure_history_unsupported_schema", "This failure-history format is not supported by this version of azclips. Explicitly reset through 'agent.recovery=off' to discard it.");
}
