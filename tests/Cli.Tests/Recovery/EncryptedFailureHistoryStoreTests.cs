// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions.Recovery;
using Core.Runtime;
using Core.Runtime.Recovery;
using Microsoft.Extensions.DependencyInjection;

namespace Cli.Tests.Recovery;

public sealed class EncryptedFailureHistoryStoreTests : IDisposable
{
    private readonly string _directory = Path.GetFullPath(
        Path.Combine(".test-artifacts", "failure-history", Guid.NewGuid().ToString("N")));

    private string HistoryPath => Path.Combine(_directory, EncryptedFailureHistoryStore.HistoryFileName);
    private string LockPath => Path.Combine(_directory, EncryptedFailureHistoryStore.LockFileName);

    [Fact]
    public async Task TwelveWrites_RetainExactlyLatestTen_InInsertionOrder()
    {
        var keys = new TestKeyStore();
        var store = new EncryptedFailureHistoryStore(keys, _directory);
        var written = Enumerable.Range(0, 12)
            .Select(index => CreateRecord() with { OccurredAtUtc = new DateTimeOffset(2026, 1, 12, 0, 0, 0, TimeSpan.Zero).AddDays(-index) })
            .ToArray();

        foreach (var record in written)
        {
            Assert.Equal(new FailureHistoryEntry(record.Id, record.OccurredAtUtc), await store.SaveAsync(record, 10));
        }

        var records = await store.ReadRecordsAsync();
        var index = await store.ReadIndexAsync();
        var expected = written.Skip(2).Reverse().ToArray();
        Assert.Equal(10, records.Count);
        Assert.Equal(expected.Select(record => record.Id), records.Select(record => record.Id));
        Assert.Equal(expected.Select(record => new FailureHistoryEntry(record.Id, record.OccurredAtUtc)), index);
        Assert.True(keys.CreateRequests.First());
        Assert.All(keys.CreateRequests.Skip(1), create => Assert.False(create));
        Assert.All(keys.ReturnedKeys, key => Assert.All(key, value => Assert.Equal((byte)0, value)));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task LowerLimit_PrunesOnlyOnTheNextSuccessfulWrite()
    {
        var keys = new TestKeyStore();
        var store = new EncryptedFailureHistoryStore(keys, _directory);
        var written = Enumerable.Range(0, 12).Select(_ => CreateRecord()).ToArray();
        foreach (var record in written)
        {
            await store.SaveAsync(record, 20);
        }

        var original = await File.ReadAllBytesAsync(HistoryPath);
        var anotherInstance = new EncryptedFailureHistoryStore(keys, _directory);
        Assert.Equal(12, (await anotherInstance.ReadRecordsAsync()).Count);
        Assert.Equal(12, (await anotherInstance.ReadIndexAsync()).Count);
        Assert.Equal(original, await File.ReadAllBytesAsync(HistoryPath));

        var next = CreateRecord();
        await anotherInstance.SaveAsync(next, 3);
        Assert.Equal(
            new[] { next.Id, written[^1].Id, written[^2].Id },
            (await store.ReadRecordsAsync()).Select(record => record.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task NonPositiveLimit_DoesNotCreateHistoryOrAccessKeys(int limit)
    {
        var keys = new TestKeyStore();
        var store = new EncryptedFailureHistoryStore(keys, _directory);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.SaveAsync(CreateRecord(), limit));

        Assert.False(Directory.Exists(_directory));
        Assert.Empty(keys.CreateRequests);
    }

    [Fact]
    public async Task LargePositiveLimit_DoesNotAllocateTheConfiguredCapacity()
    {
        var store = new EncryptedFailureHistoryStore(new TestKeyStore(), _directory);
        await store.SaveAsync(CreateRecord(), int.MaxValue);
        Assert.Single(await store.ReadRecordsAsync());
    }

    [Fact]
    public async Task ConstructorsAndAbsentHistoryReads_AreSideEffectFree()
    {
        var keys = new TestKeyStore { Read = (_, _) => throw new InvalidOperationException("Keys must not be accessed.") };
        var store = new EncryptedFailureHistoryStore(keys, _directory);

        Assert.False(Directory.Exists(_directory));
        Assert.Empty(await store.ReadIndexAsync());
        Assert.Empty(await store.ReadRecordsAsync());
        Assert.False(Directory.Exists(_directory));
        Assert.Empty(keys.CreateRequests);
    }

    [Fact]
    public async Task IndexRead_DoesNotAccessKeysOrCreateALockFile()
    {
        var original = CreateRecord();
        await new EncryptedFailureHistoryStore(new TestKeyStore(), _directory).SaveAsync(original, 10);
        File.Delete(LockPath);
        var bytes = await File.ReadAllBytesAsync(HistoryPath);
        var keys = new TestKeyStore { Read = (_, _) => throw new InvalidOperationException("Keys must not be accessed.") };

        var index = await new EncryptedFailureHistoryStore(keys, _directory).ReadIndexAsync();

        Assert.Equal(new FailureHistoryEntry(original.Id, original.OccurredAtUtc), Assert.Single(index));
        Assert.Empty(keys.CreateRequests);
        Assert.False(File.Exists(LockPath));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(HistoryPath));
    }

    [Fact]
    public async Task CompleteRawRecord_RoundTripsUnicodeQuotesNewlinesAndLongText_WithoutPlaintextOnDisk()
    {
        const string marker = "failure-history-secret-marker-8b7e489d";
        var raw = marker + string.Concat(Enumerable.Repeat(" 中文 🚀 \"quoted\" \\' $variable\r\n\t", 4_000));
        Assert.True(raw.Length > 70_000);
        using var details = JsonDocument.Parse("""{"value":"private-error-details","nested":{"status":409}}""");
        var expected = CreateRecord() with
        {
            Arguments = ["vm", "create", "--raw", raw, "", "\r\n", "\"literal quotes\""],
            FailedCommand = $"az vm create --raw \"{raw}\"",
            ErrorText = raw + "\nlast diagnostic line",
            CommandName = "vm create",
            ErrorCode = "Conflict",
            ExceptionType = "Contoso.CommandException",
            ErrorDetails = details.RootElement.Clone(),
            HttpStatusCode = 409,
            WorkingDirectory = @"C:\Users\某人\quoted directory",
            ProfileName = "private-profile",
            ClientVersion = "1.2.3",
            CommandTraceId = "0123456789abcdef0123456789abcdef",
            CommandSpanId = "0123456789abcdef",
            ExitCode = -17,
        };
        var keys = new TestKeyStore();
        await new EncryptedFailureHistoryStore(keys, _directory).SaveAsync(expected, 10);

        var actual = Assert.Single(await new EncryptedFailureHistoryStore(keys, _directory).ReadRecordsAsync());

        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        foreach (var path in Directory.GetFiles(_directory))
        {
            var file = await File.ReadAllBytesAsync(path);
            Assert.True(file.AsSpan().IndexOf(Encoding.UTF8.GetBytes(marker)) < 0);
            Assert.True(file.AsSpan().IndexOf(Encoding.UTF8.GetBytes("private-error-details")) < 0);
            Assert.True(file.AsSpan().IndexOf(Encoding.UTF8.GetBytes("private-profile")) < 0);
            Assert.True(file.AsSpan().IndexOf(keys.Key) < 0);
        }
    }

    [Fact]
    public async Task EmptyRawTextAndArguments_ArePreservedRatherThanInvented()
    {
        var store = new EncryptedFailureHistoryStore(new TestKeyStore(), _directory);
        await store.SaveAsync(CreateRecord() with { Arguments = [], FailedCommand = "", ErrorText = "" }, 10);
        var result = Assert.Single(await store.ReadRecordsAsync());
        Assert.Empty(result.Arguments);
        Assert.Empty(result.FailedCommand);
        Assert.Empty(result.ErrorText);
    }

    [Fact]
    public async Task EverySuccessfulWrite_UsesAFreshNonce()
    {
        var store = new EncryptedFailureHistoryStore(new TestKeyStore(), _directory);
        await store.SaveAsync(CreateRecord(), 1);
        var first = await File.ReadAllBytesAsync(HistoryPath);
        await store.SaveAsync(CreateRecord(), 1);
        var second = await File.ReadAllBytesAsync(HistoryPath);
        var nonceOffset = FailureHistorySnapshot.HeaderSize + FailureHistorySnapshot.IndexEntrySize;
        Assert.False(first.AsSpan(nonceOffset, FailureHistorySnapshot.NonceSize)
            .SequenceEqual(second.AsSpan(nonceOffset, FailureHistorySnapshot.NonceSize)));
    }

    [Fact]
    public async Task DuplicateId_IsRejectedWithoutChangingTheSnapshot()
    {
        var store = new EncryptedFailureHistoryStore(new TestKeyStore(), _directory);
        var record = CreateRecord();
        await store.SaveAsync(record, 10);
        var original = await File.ReadAllBytesAsync(HistoryPath);

        var error = await Assert.ThrowsAsync<FailureHistoryException>(() =>
            store.SaveAsync(record with { ErrorText = "replacement must not happen" }, 1));

        Assert.Equal("failure_history_duplicate_id", error.Code);
        Assert.Equal(original, await File.ReadAllBytesAsync(HistoryPath));
        Assert.Equal(record.ErrorText, Assert.Single(await store.ReadRecordsAsync()).ErrorText);
    }

    [Theory]
    [InlineData("null-id")]
    [InlineData("empty-id")]
    [InlineData("noncanonical-id")]
    [InlineData("uppercase-id")]
    [InlineData("missing-time")]
    [InlineData("non-utc-time")]
    [InlineData("null-arguments")]
    [InlineData("null-argument")]
    [InlineData("null-command")]
    [InlineData("null-error")]
    [InlineData("success-exit-code")]
    public async Task InvalidRequiredFields_AreRejectedBeforeAnyIO(string invalidField)
    {
        var record = CreateRecord();
        record = invalidField switch
        {
            "null-id" => record with { Id = null! },
            "empty-id" => record with { Id = Guid.Empty.ToString("N") },
            "noncanonical-id" => record with { Id = Guid.NewGuid().ToString("D") },
            "uppercase-id" => record with { Id = "0123456789ABCDEF0123456789ABCDEF" },
            "missing-time" => record with { OccurredAtUtc = default },
            "non-utc-time" => record with { OccurredAtUtc = record.OccurredAtUtc.ToOffset(TimeSpan.FromHours(1)) },
            "null-arguments" => record with { Arguments = null! },
            "null-argument" => record with { Arguments = [null!] },
            "null-command" => record with { FailedCommand = null! },
            "null-error" => record with { ErrorText = null! },
            "success-exit-code" => record with { ExitCode = 0 },
            _ => throw new ArgumentException(nameof(invalidField)),
        };
        var keys = new TestKeyStore();
        var store = new EncryptedFailureHistoryStore(keys, _directory);

        var error = await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(record, 10));

        Assert.Equal("failure_history_invalid_record", error.Code);
        Assert.False(Directory.Exists(_directory));
        Assert.Empty(keys.CreateRequests);
    }

    [Fact]
    public async Task UnsupportedRecordSchema_IsRejectedBeforeAnyIO()
    {
        var keys = new TestKeyStore();
        var store = new EncryptedFailureHistoryStore(keys, _directory);
        var error = await Assert.ThrowsAsync<FailureHistoryException>(() =>
            store.SaveAsync(CreateRecord() with { SchemaVersion = 2 }, 10));
        Assert.Equal("failure_history_unsupported_schema", error.Code);
        Assert.False(Directory.Exists(_directory));
        Assert.Empty(keys.CreateRequests);
    }

    [Fact]
    public async Task WrongKey_FailsClosedForReadsAndWrites_AndClearsOwnedBytes()
    {
        await new EncryptedFailureHistoryStore(new TestKeyStore(), _directory).SaveAsync(CreateRecord(), 10);
        var original = await File.ReadAllBytesAsync(HistoryPath);
        var wrongKeys = new TestKeyStore([.. Enumerable.Repeat((byte)0x77, 32)]);
        var store = new EncryptedFailureHistoryStore(wrongKeys, _directory);

        Assert.Equal("failure_history_authentication_failed",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadRecordsAsync())).Code);
        Assert.Equal("failure_history_authentication_failed",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 10))).Code);

        Assert.Equal(original, await File.ReadAllBytesAsync(HistoryPath));
        Assert.All(wrongKeys.CreateRequests, create => Assert.False(create));
        Assert.All(wrongKeys.ReturnedKeys, key => Assert.All(key, value => Assert.Equal((byte)0, value)));
        Assert.Single(await store.ReadIndexAsync());
    }

    [Fact]
    public async Task MissingExistingKey_IsNeverRegenerated()
    {
        await new EncryptedFailureHistoryStore(new TestKeyStore(), _directory).SaveAsync(CreateRecord(), 10);
        var original = await File.ReadAllBytesAsync(HistoryPath);
        var keys = new TestKeyStore
        {
            Read = (_, _) => throw new FailureHistoryException("failure_history_key_missing", "Protected key is missing."),
        };
        var store = new EncryptedFailureHistoryStore(keys, _directory);

        Assert.Single(await store.ReadIndexAsync());
        Assert.Empty(keys.CreateRequests);
        Assert.Equal("failure_history_key_missing",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadRecordsAsync())).Code);
        Assert.Equal("failure_history_key_missing",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 10))).Code);
        Assert.All(keys.CreateRequests, create => Assert.False(create));
        Assert.Equal(original, await File.ReadAllBytesAsync(HistoryPath));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    public async Task InvalidKeySize_IsRejectedAndCleared(int length)
    {
        var keys = new TestKeyStore([.. Enumerable.Repeat((byte)0x44, length)]);
        var store = new EncryptedFailureHistoryStore(keys, _directory);

        Assert.Equal("failure_history_key_invalid",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 10))).Code);

        Assert.False(File.Exists(HistoryPath));
        Assert.All(keys.ReturnedKeys, key => Assert.All(key, value => Assert.Equal((byte)0, value)));
    }

    [Fact]
    public async Task NativeProviderDiagnostics_AreNotExposed()
    {
        const string sensitive = "native-key-secret-and-account-details";
        var keys = new TestKeyStore { Read = (_, _) => throw new IOException(sensitive) };
        var store = new EncryptedFailureHistoryStore(keys, _directory);

        var error = await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 10));

        Assert.Equal("failure_history_key_unavailable", error.Code);
        Assert.DoesNotContain(sensitive, error.ToString(), StringComparison.Ordinal);
        Assert.Null(error.InnerException);
        Assert.False(File.Exists(HistoryPath));
    }

    [Theory]
    [InlineData("invalid-operation")]
    [InlineData("argument")]
    [InlineData("null-reference")]
    [InlineData("json")]
    public async Task UnexpectedKeyProviderDefects_AreNotClassifiedAsStorageFailures(string kind)
    {
        Exception expected = kind switch
        {
            "invalid-operation" => new InvalidOperationException("Provider configuration defect."),
            "argument" => new ArgumentException("Provider programming defect."),
            "null-reference" => new NullReferenceException("Provider programming defect."),
            "json" => new JsonException("Provider programming defect."),
            _ => throw new ArgumentException(nameof(kind)),
        };
        var keys = new TestKeyStore { Read = (_, _) => throw expected };
        var store = new EncryptedFailureHistoryStore(keys, _directory);

        var actual = await Record.ExceptionAsync(() => store.SaveAsync(CreateRecord(), 10));

        Assert.Same(expected, actual);
        Assert.False(File.Exists(HistoryPath));
    }

    [Theory]
    [InlineData("ciphertext")]
    [InlineData("nonce")]
    [InlineData("tag")]
    [InlineData("valid-index-id")]
    [InlineData("valid-index-time")]
    public async Task AuthenticatedBytes_TamperingCannotBeReadOrOverwritten(string target)
    {
        var keys = new TestKeyStore();
        var store = new EncryptedFailureHistoryStore(keys, _directory);
        await store.SaveAsync(CreateRecord(), 10);
        var bytes = await File.ReadAllBytesAsync(HistoryPath);
        var headerLength = FailureHistorySnapshot.HeaderSize + FailureHistorySnapshot.IndexEntrySize;
        switch (target)
        {
            case "ciphertext":
                bytes[^1] ^= 0x80;
                break;
            case "nonce":
                bytes[headerLength] ^= 0x80;
                break;
            case "tag":
                bytes[headerLength + FailureHistorySnapshot.NonceSize] ^= 0x80;
                break;
            case "valid-index-id":
                Guid.NewGuid().TryWriteBytes(bytes.AsSpan(FailureHistorySnapshot.HeaderSize, 16));
                break;
            case "valid-index-time":
                var ticks = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(FailureHistorySnapshot.HeaderSize + 16));
                BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(FailureHistorySnapshot.HeaderSize + 16), ticks + 1);
                break;
        }

        await File.WriteAllBytesAsync(HistoryPath, bytes);
        var keyCalls = keys.CreateRequests.Count;
        Assert.Single(await store.ReadIndexAsync());
        Assert.Equal(keyCalls, keys.CreateRequests.Count);
        Assert.Equal("failure_history_authentication_failed",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadRecordsAsync())).Code);
        Assert.Equal("failure_history_authentication_failed",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 1))).Code);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(HistoryPath));
    }

    [Theory]
    [InlineData("magic")]
    [InlineData("empty-guid")]
    [InlineData("invalid-time")]
    [InlineData("negative-count")]
    [InlineData("excessive-count")]
    [InlineData("invalid-length")]
    [InlineData("truncated")]
    [InlineData("trailing")]
    [InlineData("empty-file")]
    public async Task MalformedEnvelope_FailsWithoutKeyAccessOrReset(string target)
    {
        await new EncryptedFailureHistoryStore(new TestKeyStore(), _directory).SaveAsync(CreateRecord(), 10);
        var bytes = await File.ReadAllBytesAsync(HistoryPath);
        switch (target)
        {
            case "magic": bytes[0] ^= 1; break;
            case "empty-guid": bytes.AsSpan(FailureHistorySnapshot.HeaderSize, 16).Clear(); break;
            case "invalid-time": BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(FailureHistorySnapshot.HeaderSize + 16), long.MaxValue); break;
            case "negative-count": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), -1); break;
            case "excessive-count": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), int.MaxValue); break;
            case "invalid-length": BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(16), long.MaxValue); break;
            case "truncated": bytes = bytes[..^1]; break;
            case "trailing": bytes = [.. bytes, 1]; break;
            case "empty-file": bytes = []; break;
        }

        await File.WriteAllBytesAsync(HistoryPath, bytes);
        var keys = new TestKeyStore();
        var store = new EncryptedFailureHistoryStore(keys, _directory);

        Assert.Equal("failure_history_corrupt",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadIndexAsync())).Code);
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadRecordsAsync());
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 1));
        Assert.Empty(keys.CreateRequests);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(HistoryPath));
    }

    [Fact]
    public async Task UnsupportedEnvelopeVersion_FailsWithoutKeyAccess()
    {
        await new EncryptedFailureHistoryStore(new TestKeyStore(), _directory).SaveAsync(CreateRecord(), 10);
        var bytes = await File.ReadAllBytesAsync(HistoryPath);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 2);
        await File.WriteAllBytesAsync(HistoryPath, bytes);
        var keys = new TestKeyStore();
        var store = new EncryptedFailureHistoryStore(keys, _directory);

        Assert.Equal("failure_history_unsupported_schema",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadIndexAsync())).Code);
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 10));
        Assert.Empty(keys.CreateRequests);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(HistoryPath));
    }

    [Fact]
    public async Task DuplicateIndexIds_AreRejectedWithoutKeyAccess()
    {
        var store = new EncryptedFailureHistoryStore(new TestKeyStore(), _directory);
        await store.SaveAsync(CreateRecord(), 10);
        await store.SaveAsync(CreateRecord(), 10);
        var bytes = await File.ReadAllBytesAsync(HistoryPath);
        bytes.AsSpan(FailureHistorySnapshot.HeaderSize, 16)
            .CopyTo(bytes.AsSpan(FailureHistorySnapshot.HeaderSize + FailureHistorySnapshot.IndexEntrySize, 16));
        await File.WriteAllBytesAsync(HistoryPath, bytes);
        var keys = new TestKeyStore();

        Assert.Equal("failure_history_corrupt",
            (await Assert.ThrowsAsync<FailureHistoryException>(() =>
                new EncryptedFailureHistoryStore(keys, _directory).ReadIndexAsync())).Code);
        Assert.Empty(keys.CreateRequests);
    }

    [Theory]
    [InlineData("unsupported-record-schema")]
    [InlineData("missing-record-schema")]
    [InlineData("missing-required-field")]
    [InlineData("success-exit-code")]
    [InlineData("different-index-id")]
    [InlineData("different-index-time")]
    [InlineData("duplicate-record-id")]
    [InlineData("different-count")]
    [InlineData("null-record")]
    public async Task AuthenticatedButInvalidPayload_FailsClosed(string invalidPayload)
    {
        var keys = new TestKeyStore();
        var store = new EncryptedFailureHistoryStore(keys, _directory);
        await store.SaveAsync(CreateRecord(), 10);
        await store.SaveAsync(CreateRecord(), 10);
        await RewritePayloadAsync(keys.Key, records =>
        {
            var first = records[0]!.AsObject();
            switch (invalidPayload)
            {
                case "unsupported-record-schema": first["schemaVersion"] = 2; break;
                case "missing-record-schema": first.Remove("schemaVersion"); break;
                case "missing-required-field": first.Remove("errorText"); break;
                case "success-exit-code": first["exitCode"] = 0; break;
                case "different-index-id": first["id"] = Guid.NewGuid().ToString("N"); break;
                case "different-index-time": first["occurredAtUtc"] = DateTimeOffset.UtcNow.AddYears(-1); break;
                case "duplicate-record-id": records[1]!["id"] = first["id"]!.GetValue<string>(); break;
                case "different-count": records.RemoveAt(1); break;
                case "null-record": records[0] = null; break;
            }
        });
        var original = await File.ReadAllBytesAsync(HistoryPath);

        await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadRecordsAsync());
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 1));
        Assert.Equal(original, await File.ReadAllBytesAsync(HistoryPath));
    }

    [Fact]
    public async Task SeparateInstances_ConcurrentWritersDoNotLoseRecords()
    {
        var keys = new TestKeyStore();
        var records = Enumerable.Range(0, 12).Select(_ => CreateRecord()).ToArray();
        await Task.WhenAll(records.Select(record => Task.Run(() =>
            new EncryptedFailureHistoryStore(keys, _directory).SaveAsync(record, 12))));

        var loaded = await new EncryptedFailureHistoryStore(keys, _directory).ReadRecordsAsync();
        Assert.Equal(records.Select(record => record.Id).Order(), loaded.Select(record => record.Id).Order());
        Assert.Equal(1, keys.CreateRequests.Count(create => create));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task FailedAtomicReplacement_PreservesOldSnapshotAndRemovesEncryptedStagingFile()
    {
        var keys = new TestKeyStore();
        var originalRecord = CreateRecord();
        await new EncryptedFailureHistoryStore(keys, _directory).SaveAsync(originalRecord, 10);
        var original = await File.ReadAllBytesAsync(HistoryPath);
        var store = new EncryptedFailureHistoryStore(keys, _directory, TimeSpan.FromSeconds(1),
            (_, _) => throw new IOException("private filesystem diagnostic"));

        var error = await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 1));

        Assert.Equal("failure_history_io", error.Code);
        Assert.DoesNotContain("private filesystem diagnostic", error.ToString(), StringComparison.Ordinal);
        Assert.Null(error.InnerException);
        Assert.Equal(original, await File.ReadAllBytesAsync(HistoryPath));
        Assert.Equal(originalRecord.Id, Assert.Single(await store.ReadRecordsAsync()).Id);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        Assert.All(keys.ReturnedKeys, key => Assert.All(key, value => Assert.Equal((byte)0, value)));
    }

    [WindowsProtectedStoreFact]
    public async Task DeniedWindowsAtomicReplacement_PreservesOldSnapshot()
    {
        var store = new EncryptedFailureHistoryStore(new TestKeyStore(), _directory);
        var originalRecord = CreateRecord();
        await store.SaveAsync(originalRecord, 10);
        var original = await File.ReadAllBytesAsync(HistoryPath);
        using (var denyReplacement = new FileStream(HistoryPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Equal("failure_history_io",
                (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 1))).Code);
        }

        Assert.Equal(original, await File.ReadAllBytesAsync(HistoryPath));
        Assert.Equal(originalRecord.Id, Assert.Single(await store.ReadRecordsAsync()).Id);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task IndexReads_DoNotWaitForTheWriterLock()
    {
        var record = CreateRecord();
        await new EncryptedFailureHistoryStore(new TestKeyStore(), _directory).SaveAsync(record, 10);
        using var heldLock = new FileStream(LockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var keys = new TestKeyStore { Read = (_, _) => throw new InvalidOperationException("Unexpected key access.") };

        var entry = Assert.Single(await new EncryptedFailureHistoryStore(keys, _directory).ReadIndexAsync());

        Assert.Equal(record.Id, entry.Id);
        Assert.Empty(keys.CreateRequests);
    }

    [Fact]
    public async Task InvalidStorageTarget_IsNotTreatedAsAbsentHistory()
    {
        Directory.CreateDirectory(HistoryPath);
        var keys = new TestKeyStore();
        var store = new EncryptedFailureHistoryStore(keys, _directory);

        Assert.Equal("failure_history_io",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadIndexAsync())).Code);
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadRecordsAsync());
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 10));
        Assert.Empty(keys.CreateRequests);
    }

    [Fact]
    public async Task WriterLock_HasABoundedWait()
    {
        Directory.CreateDirectory(_directory);
        using var heldLock = new FileStream(LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var keys = new TestKeyStore();
        var store = new EncryptedFailureHistoryStore(keys, _directory, TimeSpan.FromMilliseconds(80),
            static (source, target) => File.Move(source, target, overwrite: true));
        var watch = Stopwatch.StartNew();

        var error = await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 10));

        Assert.Equal("failure_history_locked", error.Code);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3));
        Assert.Empty(keys.CreateRequests);
        Assert.False(File.Exists(HistoryPath));
    }

    [Fact]
    public async Task WaitingForWriterLock_IsCancellable()
    {
        Directory.CreateDirectory(_directory);
        using var heldLock = new FileStream(LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(80));
        var keys = new TestKeyStore();
        var store = new EncryptedFailureHistoryStore(keys, _directory);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(CreateRecord(), 10, cancellation.Token));

        Assert.Empty(keys.CreateRequests);
        Assert.False(File.Exists(HistoryPath));
    }

    [Fact]
    public async Task AlreadyCancelledOperations_HaveNoSideEffects()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var keys = new TestKeyStore();
        var store = new EncryptedFailureHistoryStore(keys, _directory);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(CreateRecord(), 10, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadIndexAsync(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadRecordsAsync(cancellation.Token));
        Assert.False(Directory.Exists(_directory));
        Assert.Empty(keys.CreateRequests);
    }

    [Fact]
    public async Task CancellationAfterKeyRetrieval_StillClearsOwnedKey()
    {
        using var cancellation = new CancellationTokenSource();
        var ownedKey = Enumerable.Repeat((byte)0x55, 32).ToArray();
        var keys = new TestKeyStore
        {
            Read = (_, _) =>
            {
                cancellation.Cancel();
                return Task.FromResult(ownedKey);
            },
        };
        var store = new EncryptedFailureHistoryStore(keys, _directory);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(CreateRecord(), 10, cancellation.Token));

        Assert.All(ownedKey, value => Assert.Equal((byte)0, value));
        Assert.False(File.Exists(HistoryPath));
    }

    [Fact]
    public void RuntimeRegistration_UsesAbstractionWithoutAccessingKeys()
    {
        var keys = new TestKeyStore { Read = (_, _) => throw new InvalidOperationException("Unexpected key access.") };
        var services = new ServiceCollection();
        services.AddSingleton<IFailureHistoryKeyStore>(keys);
        services.RegisterRuntimeServices();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<EncryptedFailureHistoryStore>(provider.GetRequiredService<IFailureHistoryStore>());
        Assert.Empty(keys.CreateRequests);
    }

    private async Task RewritePayloadAsync(byte[] key, Action<JsonArray> change)
    {
        var original = await File.ReadAllBytesAsync(HistoryPath);
        var count = BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(12));
        var headerLength = FailureHistorySnapshot.HeaderSize + count * FailureHistorySnapshot.IndexEntrySize;
        var ciphertextOffset = headerLength + FailureHistorySnapshot.NonceSize + FailureHistorySnapshot.TagSize;
        var plaintext = new byte[original.Length - ciphertextOffset];
        byte[]? modified = null;
        try
        {
            using var aes = new AesGcm(key, FailureHistorySnapshot.TagSize);
            aes.Decrypt(
                original.AsSpan(headerLength, FailureHistorySnapshot.NonceSize),
                original.AsSpan(ciphertextOffset),
                original.AsSpan(headerLength + FailureHistorySnapshot.NonceSize, FailureHistorySnapshot.TagSize),
                plaintext,
                original.AsSpan(0, headerLength));
            var records = JsonNode.Parse(plaintext)!.AsArray();
            change(records);
            modified = JsonSerializer.SerializeToUtf8Bytes(records);
            var rewritten = new byte[ciphertextOffset + modified.Length];
            original.AsSpan(0, headerLength).CopyTo(rewritten);
            BinaryPrimitives.WriteInt64LittleEndian(rewritten.AsSpan(16), modified.Length);
            RandomNumberGenerator.Fill(rewritten.AsSpan(headerLength, FailureHistorySnapshot.NonceSize));
            aes.Encrypt(
                rewritten.AsSpan(headerLength, FailureHistorySnapshot.NonceSize),
                modified,
                rewritten.AsSpan(ciphertextOffset),
                rewritten.AsSpan(headerLength + FailureHistorySnapshot.NonceSize, FailureHistorySnapshot.TagSize),
                rewritten.AsSpan(0, headerLength));
            await File.WriteAllBytesAsync(HistoryPath, rewritten);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (modified is not null)
            {
                CryptographicOperations.ZeroMemory(modified);
            }
        }
    }

    private static FailureRecord CreateRecord() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        OccurredAtUtc = new DateTimeOffset(2026, 9, 21, 10, 0, 0, TimeSpan.Zero),
        Arguments = ["group", "create", "--name", "private-resource-name"],
        FailedCommand = "az group create --name private-resource-name",
        ErrorText = "The requested operation failed.",
        ExitCode = 1,
    };

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class TestKeyStore(byte[]? key = null) : IFailureHistoryKeyStore
    {
        internal byte[] Key { get; } = key ?? [.. Enumerable.Repeat((byte)0x31, 32)];
        internal ConcurrentQueue<bool> CreateRequests { get; } = new();
        internal ConcurrentBag<byte[]> ReturnedKeys { get; } = [];
        internal Func<bool, CancellationToken, Task<byte[]>>? Read { get; init; }

        public async Task<byte[]> GetKeyAsync(bool createIfMissing, CancellationToken cancellationToken = default)
        {
            CreateRequests.Enqueue(createIfMissing);
            var result = Read is null ? Key.ToArray() : await Read(createIfMissing, cancellationToken);
            ReturnedKeys.Add(result);
            return result;
        }
    }
}
