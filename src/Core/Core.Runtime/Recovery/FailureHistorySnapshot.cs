// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers.Binary;
using Core.Abstractions.Recovery;

namespace Core.Runtime.Recovery;

internal sealed class FailureHistorySnapshot
{
    // v1: magic[8], version:int32, count:int32, ciphertextLength:int64, index[count],
    // nonce[12], tag[16], ciphertext. Each index entry is a GUID[16] and UTC ticks:int64.
    // All integers are little-endian; the complete header and index are AES-GCM AAD.
    internal const int HeaderSize = 24;
    internal const int IndexEntrySize = 24;
    internal const int NonceSize = 12;
    internal const int TagSize = 16;
    private const int EnvelopeVersion = 1;
    private static ReadOnlySpan<byte> Magic => "AZCLFHST"u8;

    private FailureHistorySnapshot(byte[] authenticatedHeader, IReadOnlyList<FailureHistoryEntry> entries)
    {
        AuthenticatedHeader = authenticatedHeader;
        Entries = entries;
    }

    internal byte[] AuthenticatedHeader { get; }
    internal IReadOnlyList<FailureHistoryEntry> Entries { get; }
    internal byte[]? Nonce { get; private set; }
    internal byte[]? Tag { get; private set; }
    internal byte[]? Ciphertext { get; private set; }

    internal static byte[] CreateHeader(IReadOnlyList<FailureRecord> records, int ciphertextLength)
    {
        var header = new byte[checked(HeaderSize + records.Count * IndexEntrySize)];
        Magic.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8), EnvelopeVersion);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12), records.Count);
        BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(16), ciphertextLength);
        for (var index = 0; index < records.Count; index++)
        {
            var entry = header.AsSpan(HeaderSize + index * IndexEntrySize, IndexEntrySize);
            Guid.ParseExact(records[index].Id, "N").TryWriteBytes(entry);
            BinaryPrimitives.WriteInt64LittleEndian(entry[16..], records[index].OccurredAtUtc.UtcTicks);
        }

        return header;
    }

    internal static async Task<FailureHistorySnapshot> ReadAsync(
        FileStream stream,
        bool includeCiphertext,
        CancellationToken cancellationToken)
    {
        try
        {
            var fixedHeader = new byte[HeaderSize];
            await stream.ReadExactlyAsync(fixedHeader, cancellationToken).ConfigureAwait(false);
            if (!fixedHeader.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            {
                throw Corrupt();
            }

            if (BinaryPrimitives.ReadInt32LittleEndian(fixedHeader.AsSpan(8)) != EnvelopeVersion)
            {
                throw UnsupportedSchema();
            }

            var count = BinaryPrimitives.ReadInt32LittleEndian(fixedHeader.AsSpan(12));
            var ciphertextLength = BinaryPrimitives.ReadInt64LittleEndian(fixedHeader.AsSpan(16));
            var headerLength = HeaderSize + (long)count * IndexEntrySize;
            if (count <= 0 || headerLength > Array.MaxLength ||
                ciphertextLength <= 0 || ciphertextLength > Array.MaxLength ||
                stream.Length != headerLength + NonceSize + TagSize + ciphertextLength)
            {
                throw Corrupt();
            }

            var header = new byte[(int)headerLength];
            fixedHeader.CopyTo(header, 0);
            await stream.ReadExactlyAsync(header.AsMemory(HeaderSize), cancellationToken).ConfigureAwait(false);
            var entries = new FailureHistoryEntry[count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < count; index++)
            {
                var entry = header.AsSpan(HeaderSize + index * IndexEntrySize, IndexEntrySize);
                var id = new Guid(entry[..16]);
                var ticks = BinaryPrimitives.ReadInt64LittleEndian(entry[16..]);
                if (id == Guid.Empty || ticks <= 0 || ticks > DateTimeOffset.MaxValue.UtcTicks)
                {
                    throw Corrupt();
                }

                var canonicalId = id.ToString("N");
                if (!ids.Add(canonicalId))
                {
                    throw Corrupt();
                }

                entries[index] = new FailureHistoryEntry(canonicalId, new DateTimeOffset(ticks, TimeSpan.Zero));
            }

            var snapshot = new FailureHistorySnapshot(header, entries);
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
        catch (EndOfStreamException)
        {
            throw Corrupt();
        }
    }

    internal static bool IsCanonicalId(string? id)
        => id is not null && Guid.TryParseExact(id, "N", out var parsed) &&
           parsed != Guid.Empty && string.Equals(id, parsed.ToString("N"), StringComparison.Ordinal);

    internal static FailureHistoryException Corrupt()
        => new("failure_history_corrupt", "The encrypted failure-history snapshot is malformed or incomplete.");

    internal static FailureHistoryException UnsupportedSchema()
        => new("failure_history_unsupported_schema", "This failure-history format is not supported by this version of azclips.");
}
