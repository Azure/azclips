// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions.Recovery;
using Core.Runtime.Recovery;
using static Cli.Tests.Recovery.FailureHistoryTestScope;

namespace Cli.Tests.Recovery;

[Collection("Azclips Service Provider Tests")]
public sealed class EncryptedFailureHistoryStoreTests
{
    [Fact]
    public async Task TwelveWrites_RetainExactlyLatestTenBySequenceDespiteClockRollback()
    {
        using var scope = new FailureHistoryTestScope();
        var keys = new TestFailureHistoryKeys();
        var store = scope.CreateStore(keys);
        var written = new List<FailureRecord>();
        for (var index = 0; index < 12; index++)
        {
            var record = CreateRecord() with { OccurredAtUtc = CreateRecord().OccurredAtUtc.AddDays(-index) };
            var entry = await store.SaveAsync(record, 10);
            var committed = Committed(record, entry);
            Assert.Equal((ulong)index, committed.Sequence);
            written.Add(committed);
        }

        var records = await store.ReadRecordsAsync();
        var expected = written.Skip(2).Reverse().ToArray();
        Assert.Equal(expected.Select(record => record.Id), records.Select(record => record.Id));
        Assert.Equal(expected.Select(record => new FailureHistoryEntry(record.Id, record.OccurredAtUtc)), await store.ReadIndexAsync());
        Assert.True(keys.CreateRequests.First());
        Assert.All(keys.CreateRequests.Skip(1), create => Assert.False(create));
        AssertKeysCleared(keys);
        Assert.Empty(Directory.GetFiles(scope.DirectoryPath, "*.tmp"));
    }

    [Fact]
    public async Task LowerLimit_IsFreshUnderTheLockAndPrunesOnlyOnASuccessfulWrite()
    {
        using var scope = new FailureHistoryTestScope();
        await scope.Configuration.SetConfigurationAsync("agent.recovery.historySize", 20);
        var store = scope.CreateStore();
        var written = new List<FailureHistoryEntry>();
        for (var index = 0; index < 12; index++)
            written.Add(await store.SaveAsync(CreateRecord(), 20));
        await scope.Configuration.SetConfigurationAsync("agent.recovery.historySize", 3);
        var original = await File.ReadAllBytesAsync(scope.Files.HistoryPath);
        Assert.Equal(12, (await store.ReadRecordsAsync()).Count);
        Assert.Equal(12, (await store.ReadIndexAsync()).Count);
        Assert.Equal(original, await File.ReadAllBytesAsync(scope.Files.HistoryPath));
        var next = await scope.CreateStore().SaveAsync(CreateRecord(), 20);
        Assert.Equal(new[] { next.Id, written[^1].Id, written[^2].Id }, (await store.ReadRecordsAsync()).Select(record => record.Id));
    }

    [Fact]
    public async Task PolicyIsRereadAfterKeyAccessBeforeReservingOrCommitting()
    {
        using var scope = new FailureHistoryTestScope();
        var keys = new TestFailureHistoryKeys
        {
            Read = async (_, _, token) =>
            {
                await scope.Provider.WriteAsync(JsonNode.Parse("""{"agent":{"recovery":{"trigger":"off"}}}""")!.AsObject(), token);
                return Enumerable.Repeat((byte)0x31, 32).ToArray();
            },
        };
        var error = await Assert.ThrowsAsync<FailureHistoryException>(() => scope.CreateStore(keys).SaveAsync(CreateRecord(), 10));
        Assert.Equal("failure_history_disabled", error.Code);
        Assert.Equal(0UL, (await scope.State.ReadAsync(default))!.NextSequence);
        Assert.False(File.Exists(scope.Files.HistoryPath));
        AssertKeysCleared(keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task InvalidLimitHint_IsRejectedBeforeIO(int limit)
    {
        using var scope = new FailureHistoryTestScope();
        var keys = new TestFailureHistoryKeys();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => scope.CreateStore(keys).SaveAsync(CreateRecord(), limit));
        Assert.False(Directory.Exists(scope.Root));
        Assert.Empty(keys.CreateRequests);
    }

    [Fact]
    public async Task LargeConfiguredLimit_DoesNotAllocateTheConfiguredCapacity()
    {
        using var scope = new FailureHistoryTestScope();
        await scope.Configuration.SetConfigurationAsync("agent.recovery.historySize", int.MaxValue);
        var store = scope.CreateStore();
        await store.SaveAsync(CreateRecord(), int.MaxValue);
        Assert.Single(await store.ReadRecordsAsync());
    }

    [Fact]
    public async Task EmptyReadAndGenerationLookup_AreSideEffectFree()
    {
        using var scope = new FailureHistoryTestScope();
        var keys = NoKeyAccess();
        var store = scope.CreateStore(keys);
        Assert.Empty(await store.ReadIndexAsync());
        Assert.Empty(await store.ReadRecordsAsync());
        Assert.Equal(0UL, await store.ReadGenerationAsync());
        await store.ApplyPolicyAsync();
        Assert.False(Directory.Exists(scope.Root));
        Assert.Empty(keys.CreateRequests);
    }

    [Fact]
    public async Task IndexAndGenerationReads_DoNotWaitForOrCreateTheWriterLock()
    {
        using var scope = new FailureHistoryTestScope();
        var entry = await scope.CreateStore().SaveAsync(CreateRecord(), 10);
        var lockPath = Path.Combine(scope.DirectoryPath, EncryptedFailureHistoryStore.LockFileName);
        var original = await File.ReadAllBytesAsync(scope.Files.HistoryPath);
        var keys = NoKeyAccess();
        var reader = scope.CreateStore(keys);
        using (var held = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Equal(entry, Assert.Single(await reader.ReadIndexAsync()));
            Assert.Equal(0UL, await reader.ReadGenerationAsync());
        }
        File.Delete(lockPath);
        Assert.Equal(entry, Assert.Single(await reader.ReadIndexAsync()));
        Assert.False(File.Exists(lockPath));
        Assert.Empty(keys.CreateRequests);
        Assert.Equal(original, await File.ReadAllBytesAsync(scope.Files.HistoryPath));
    }

    [Fact]
    public async Task CompleteRecord_RoundTripsLongRawInputsWithoutPlaintextOnDisk()
    {
        using var scope = new FailureHistoryTestScope();
        const string marker = "failure-history-secret-marker-8b7e489d";
        var raw = marker + string.Concat(Enumerable.Repeat(" 中文 🚀 \"quoted\" \\' $variable\r\n\t", 4_000));
        Assert.True(raw.Length > 70_000);
        var pending = CreateRecord() with
        {
            Arguments = ["vm", "create", "--raw", raw, "", "\r\n", "\"literal quotes\""],
            FailedCommand = $"az vm create --raw \"{raw}\"",
            ErrorText = raw + "\nlast diagnostic line",
            CommandName = "vm create", ErrorCode = "Conflict", ExceptionType = "Contoso.CommandException",
            ErrorDetails = JsonSerializer.SerializeToElement(new { detail = "private-error-details" }),
            HttpStatusCode = 409, WorkingDirectory = @"C:\Users\某人\quoted directory",
            ProfileName = "private-profile", ClientVersion = "1.2.3",
            CommandTraceId = "0123456789abcdef0123456789abcdef", CommandSpanId = "0123456789abcdef", ExitCode = -17,
        };
        var keys = new TestFailureHistoryKeys();
        var entry = await scope.CreateStore(keys).SaveAsync(pending, 10);
        var actual = Assert.Single(await scope.CreateStore(keys).ReadRecordsAsync());
        Assert.Equal(JsonSerializer.Serialize(Committed(pending, entry)), JsonSerializer.Serialize(actual));
        foreach (var path in Directory.GetFiles(scope.DirectoryPath))
        {
            var bytes = await File.ReadAllBytesAsync(path);
            foreach (var text in new[] { marker, "private-error-details", "private-profile" })
                Assert.True(bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(text)) < 0);
            Assert.True(bytes.AsSpan().IndexOf(keys.Key) < 0);
        }
    }

    [Fact]
    public async Task EmptyRawTextAndArguments_AreNotInventedOrNormalized()
    {
        using var scope = new FailureHistoryTestScope();
        var store = scope.CreateStore();
        await store.SaveAsync(CreateRecord() with { Arguments = [], FailedCommand = "", ErrorText = "" }, 10);
        var result = Assert.Single(await store.ReadRecordsAsync());
        Assert.Empty(result.Arguments);
        Assert.Empty(result.FailedCommand);
        Assert.Empty(result.ErrorText);
    }

    [Fact]
    public async Task EverySuccessfulWrite_UsesAFreshNonce()
    {
        using var scope = new FailureHistoryTestScope();
        await scope.Configuration.SetConfigurationAsync("agent.recovery.historySize", 1);
        var store = scope.CreateStore();
        await store.SaveAsync(CreateRecord(), 1);
        var first = await File.ReadAllBytesAsync(scope.Files.HistoryPath);
        await store.SaveAsync(CreateRecord(), 1);
        var second = await File.ReadAllBytesAsync(scope.Files.HistoryPath);
        Assert.False(first.AsSpan(HeaderLength(first), FailureHistorySnapshot.NonceSize)
            .SequenceEqual(second.AsSpan(HeaderLength(second), FailureHistorySnapshot.NonceSize)));
    }

    [Theory]
    [InlineData("null-id")]
    [InlineData("assigned-id")]
    [InlineData("assigned-sequence")]
    [InlineData("missing-pid")]
    [InlineData("missing-time")]
    [InlineData("non-utc-time")]
    [InlineData("null-arguments")]
    [InlineData("null-argument")]
    [InlineData("null-command")]
    [InlineData("null-error")]
    [InlineData("success-exit-code")]
    public async Task InvalidPendingFields_AreRejectedBeforeIO(string field)
    {
        using var scope = new FailureHistoryTestScope();
        var record = CreateRecord();
        record = field switch
        {
            "null-id" => record with { Id = null! },
            "assigned-id" => record with { Id = FailureId.Format(record.TerminalSessionId, record.OccurredAtUtc, 0) },
            "assigned-sequence" => record with { Sequence = 1 },
            "missing-pid" => record with { TerminalSessionId = 0 },
            "missing-time" => record with { OccurredAtUtc = default },
            "non-utc-time" => record with { OccurredAtUtc = record.OccurredAtUtc.ToOffset(TimeSpan.FromHours(1)) },
            "null-arguments" => record with { Arguments = null! },
            "null-argument" => record with { Arguments = [null!] },
            "null-command" => record with { FailedCommand = null! },
            "null-error" => record with { ErrorText = null! },
            "success-exit-code" => record with { ExitCode = 0 },
            _ => throw new ArgumentException(nameof(field)),
        };
        var keys = new TestFailureHistoryKeys();
        Assert.Equal("failure_history_invalid_record",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => scope.CreateStore(keys).SaveAsync(record, 10))).Code);
        Assert.False(Directory.Exists(scope.Root));
        Assert.Empty(keys.CreateRequests);
    }

    [Fact]
    public async Task UnsupportedRecordSchema_IsRejectedBeforeIO()
    {
        using var scope = new FailureHistoryTestScope();
        Assert.Equal("failure_history_unsupported_schema",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => scope.CreateStore().SaveAsync(CreateRecord() with { SchemaVersion = 3 }, 10))).Code);
        Assert.False(Directory.Exists(scope.Root));
    }

    [Fact]
    public async Task WrongOrMissingKeys_NeverReplaceExistingHistory()
    {
        using var scope = new FailureHistoryTestScope();
        await scope.CreateStore().SaveAsync(CreateRecord(), 10);
        var original = await File.ReadAllBytesAsync(scope.Files.HistoryPath);
        var wrong = new TestFailureHistoryKeys([.. Enumerable.Repeat((byte)0x77, 32)]);
        var missing = new TestFailureHistoryKeys
        {
            Read = (_, _, _) => throw new FailureHistoryException("failure_history_key_missing", "Protected key is missing."),
        };
        foreach (var (keys, code) in new[] { (wrong, "failure_history_authentication_failed"), (missing, "failure_history_key_missing") })
        {
            var store = scope.CreateStore(keys);
            Assert.Single(await store.ReadIndexAsync());
            Assert.Equal(code, (await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadRecordsAsync())).Code);
            Assert.Equal(code, (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 10))).Code);
            Assert.All(keys.CreateRequests, create => Assert.False(create));
            AssertKeysCleared(keys);
        }
        Assert.Equal(original, await File.ReadAllBytesAsync(scope.Files.HistoryPath));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    public async Task InvalidKeySize_IsRejectedAndCleared(int length)
    {
        using var scope = new FailureHistoryTestScope();
        var keys = new TestFailureHistoryKeys([.. Enumerable.Repeat((byte)0x44, length)]);
        Assert.Equal("failure_history_key_invalid",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => scope.CreateStore(keys).SaveAsync(CreateRecord(), 10))).Code);
        Assert.False(File.Exists(scope.Files.HistoryPath));
        AssertKeysCleared(keys);
    }

    [Fact]
    public async Task NativeProviderDiagnostics_AreNotExposed()
    {
        using var scope = new FailureHistoryTestScope();
        await scope.CreateStore().SaveAsync(CreateRecord(), 10);
        const string secret = "private-native-account-secret";
        var keys = new TestFailureHistoryKeys { Read = (_, _, _) => throw new IOException(secret) };
        var error = await Assert.ThrowsAsync<FailureHistoryException>(() => scope.CreateStore(keys).ReadRecordsAsync());
        Assert.Equal("failure_history_key_unavailable", error.Code);
        Assert.DoesNotContain(secret, error.ToString());
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task UnavailableKeyStorage_KeepsOnlyLatestFailureUnencrypted()
    {
        using var scope = new FailureHistoryTestScope();
        var unavailable = new TestFailureHistoryKeys { Read = (_, _, _) => throw new IOException("no secure store") };
        var store = scope.CreateStore(unavailable);

        var first = await store.SaveAsync(CreateRecord(), 10);
        var second = await store.SaveAsync(CreateRecord() with { ErrorText = "second" }, 10);

        Assert.True(first.Unencrypted);
        Assert.True(second.Unencrypted);
        Assert.False(File.Exists(scope.Files.HistoryPath));
        var record = Assert.Single(await store.ReadRecordsAsync());
        Assert.Equal(second.Id, record.Id);
        Assert.Equal("second", record.ErrorText);
        Assert.Equal(new[] { second }, await store.ReadIndexAsync());
        Assert.Contains("second", await File.ReadAllTextAsync(scope.Files.PlaintextHistoryPath));
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(scope.Files.PlaintextHistoryPath));
    }

    [Fact]
    public async Task AvailableKeyStorage_EncryptsPendingUnencryptedFailureAndDeletesPlaintext()
    {
        using var scope = new FailureHistoryTestScope();
        var unavailable = new TestFailureHistoryKeys { Read = (_, _, _) => throw new IOException("no secure store") };
        var fallback = await scope.CreateStore(unavailable).SaveAsync(CreateRecord() with { ErrorText = "plain" }, 10);

        var store = scope.CreateStore();
        var encrypted = await store.SaveAsync(CreateRecord(), 10);

        Assert.False(encrypted.Unencrypted);
        Assert.False(File.Exists(scope.Files.PlaintextHistoryPath));
        Assert.Equal(new[] { encrypted.Id, fallback.Id }, (await store.ReadRecordsAsync()).Select(record => record.Id));
        Assert.DoesNotContain("plain", Encoding.UTF8.GetString(await File.ReadAllBytesAsync(scope.Files.HistoryPath)));
    }

    [Fact]
    public async Task UnavailableKeyStorage_ShowsUnencryptedLatestBeforeEncryptedHistoryInIndex()
    {
        using var scope = new FailureHistoryTestScope();
        var encrypted = await scope.CreateStore().SaveAsync(CreateRecord(), 10);
        var unavailable = new TestFailureHistoryKeys { Read = (_, _, _) => throw new IOException("no secure store") };
        var store = scope.CreateStore(unavailable);
        var fallback = await store.SaveAsync(CreateRecord(), 10);

        Assert.Equal(new[] { fallback, encrypted }, await store.ReadIndexAsync());
        Assert.Equal(fallback.Id, Assert.Single(await store.ReadRecordsAsync()).Id);
        Assert.True(File.Exists(scope.Files.HistoryPath));
    }

    [Fact]
    public async Task Reset_DeletesUnencryptedLatestFailure()
    {
        using var scope = new FailureHistoryTestScope();
        var unavailable = new TestFailureHistoryKeys { Read = (_, _, _) => throw new IOException("no secure store") };
        var store = scope.CreateStore(unavailable);
        await store.SaveAsync(CreateRecord(), 10);

        await store.ClearAsync();

        Assert.False(File.Exists(scope.Files.PlaintextHistoryPath));
        Assert.Empty(await store.ReadIndexAsync());
    }

    [Fact]
    public async Task InvalidKey_DoesNotFallBackToPlaintext()
    {
        using var scope = new FailureHistoryTestScope();
        var keys = new TestFailureHistoryKeys { Read = (_, _, _) => throw new FailureHistoryException("failure_history_key_missing", "missing") };
        var error = await Assert.ThrowsAsync<FailureHistoryException>(() => scope.CreateStore(keys).SaveAsync(CreateRecord(), 10));
        Assert.Equal("failure_history_key_missing", error.Code);
        Assert.False(File.Exists(scope.Files.PlaintextHistoryPath));
    }

    [Theory]
    [InlineData("invalid-operation")]
    [InlineData("argument")]
    [InlineData("null-reference")]
    [InlineData("json")]
    public async Task UnexpectedProviderDefects_AreNotMisclassified(string kind)
    {
        using var scope = new FailureHistoryTestScope();
        Exception expected = kind switch
        {
            "invalid-operation" => new InvalidOperationException("Provider defect."),
            "argument" => new ArgumentException("Provider defect."),
            "null-reference" => new NullReferenceException("Provider defect."),
            _ => new JsonException("Provider defect."),
        };
        var keys = new TestFailureHistoryKeys { Read = (_, _, _) => throw expected };
        Assert.Same(expected, await Record.ExceptionAsync(() => scope.CreateStore(keys).SaveAsync(CreateRecord(), 10)));
    }

    [Theory]
    [InlineData("ciphertext")]
    [InlineData("nonce")]
    [InlineData("tag")]
    [InlineData("id")]
    [InlineData("time")]
    [InlineData("generation")]
    public async Task AuthenticatedBytes_CannotBeTamperedOrOverwritten(string target)
    {
        using var scope = new FailureHistoryTestScope();
        var key = Enumerable.Repeat((byte)0x31, 32).ToArray();
        var keys = new TestFailureHistoryKeys(key) { Read = (_, _, _) => Task.FromResult(key.ToArray()) };
        var store = scope.CreateStore(keys);
        await store.SaveAsync(CreateRecord(), 10);
        var bytes = await File.ReadAllBytesAsync(scope.Files.HistoryPath);
        var header = HeaderLength(bytes);
        var idLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(FailureHistorySnapshot.HeaderSize));
        switch (target)
        {
            case "ciphertext": bytes[^1] ^= 0x80; break;
            case "nonce": bytes[header] ^= 0x80; break;
            case "tag": bytes[header + FailureHistorySnapshot.NonceSize] ^= 0x80; break;
            case "id": bytes[FailureHistorySnapshot.HeaderSize + 2] = (byte)'5'; break;
            case "time":
                var timeOffset = FailureHistorySnapshot.HeaderSize + 2 + idLength;
                var ticks = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(timeOffset));
                BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(timeOffset), ticks + 1);
                break;
            case "generation":
                BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(24), 1);
                await scope.State.WriteAsync((await scope.State.ReadAsync(default))! with { Generation = 1 }, default);
                break;
        }
        await File.WriteAllBytesAsync(scope.Files.HistoryPath, bytes);
        var calls = keys.CreateRequests.Count;
        Assert.Single(await store.ReadIndexAsync());
        Assert.Equal(calls, keys.CreateRequests.Count);
        Assert.Equal("failure_history_authentication_failed",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadRecordsAsync())).Code);
        var generation = await store.ReadGenerationAsync();
        Assert.Equal("failure_history_authentication_failed",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(generation), 10))).Code);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(scope.Files.HistoryPath));
    }

    [Theory]
    [InlineData("magic")]
    [InlineData("invalid-id")]
    [InlineData("invalid-time")]
    [InlineData("negative-count")]
    [InlineData("excessive-count")]
    [InlineData("invalid-length")]
    [InlineData("invalid-index-length")]
    [InlineData("truncated")]
    [InlineData("trailing")]
    [InlineData("empty-file")]
    public async Task MalformedEnvelope_FailsWithoutKeyAccess(string target)
    {
        using var scope = new FailureHistoryTestScope();
        await scope.CreateStore().SaveAsync(CreateRecord(), 10);
        var bytes = await File.ReadAllBytesAsync(scope.Files.HistoryPath);
        var idLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(FailureHistorySnapshot.HeaderSize));
        switch (target)
        {
            case "magic": bytes[0] ^= 1; break;
            case "invalid-id": bytes[FailureHistorySnapshot.HeaderSize + 2] = (byte)'X'; break;
            case "invalid-time": BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(FailureHistorySnapshot.HeaderSize + 2 + idLength), long.MaxValue); break;
            case "negative-count": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), -1); break;
            case "excessive-count": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), int.MaxValue); break;
            case "invalid-length": BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(16), long.MaxValue); break;
            case "invalid-index-length": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(32), int.MaxValue); break;
            case "truncated": bytes = bytes[..^1]; break;
            case "trailing": bytes = [.. bytes, 1]; break;
            case "empty-file": bytes = []; break;
        }
        await File.WriteAllBytesAsync(scope.Files.HistoryPath, bytes);
        var keys = NoKeyAccess();
        var store = scope.CreateStore(keys);
        Assert.Equal("failure_history_corrupt", (await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadIndexAsync())).Code);
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadRecordsAsync());
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 10));
        Assert.Empty(keys.CreateRequests);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(scope.Files.HistoryPath));
    }

    [Fact]
    public async Task UnsupportedEnvelopeVersion_IsPreservedWithoutKeys()
    {
        using var scope = new FailureHistoryTestScope();
        await scope.CreateStore().SaveAsync(CreateRecord(), 10);
        var bytes = await File.ReadAllBytesAsync(scope.Files.HistoryPath);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 3);
        await File.WriteAllBytesAsync(scope.Files.HistoryPath, bytes);
        var store = scope.CreateStore(NoKeyAccess());
        Assert.Equal("failure_history_unsupported_schema",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadIndexAsync())).Code);
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 10));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(scope.Files.HistoryPath));
    }

    [Fact]
    public async Task DuplicateIndexSequences_FailBeforeKeyAccess()
    {
        using var scope = new FailureHistoryTestScope();
        var store = scope.CreateStore();
        await store.SaveAsync(CreateRecord(), 10);
        await store.SaveAsync(CreateRecord(), 10);
        var bytes = await File.ReadAllBytesAsync(scope.Files.HistoryPath);
        var start = FailureHistorySnapshot.HeaderSize;
        var length = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(start));
        bytes.AsSpan(start + 2, length).CopyTo(bytes.AsSpan(start + 2 + length + 8 + 2, length));
        await File.WriteAllBytesAsync(scope.Files.HistoryPath, bytes);
        Assert.Equal("failure_history_corrupt",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => scope.CreateStore(NoKeyAccess()).ReadIndexAsync())).Code);
    }

    [Theory]
    [InlineData("unsupported-schema")]
    [InlineData("missing-schema")]
    [InlineData("missing-required-field")]
    [InlineData("missing-sequence")]
    [InlineData("success-exit-code")]
    [InlineData("different-pid")]
    [InlineData("different-generation")]
    [InlineData("different-sequence")]
    [InlineData("different-count")]
    [InlineData("null-record")]
    public async Task AuthenticatedButInvalidPayload_FailsClosed(string target)
    {
        using var scope = new FailureHistoryTestScope();
        var keys = new TestFailureHistoryKeys();
        var store = scope.CreateStore(keys);
        await store.SaveAsync(CreateRecord(), 10);
        await RewritePayloadAsync(scope.Files.HistoryPath, keys.Key, records =>
        {
            var first = records[0]!.AsObject();
            switch (target)
            {
                case "unsupported-schema": first["schemaVersion"] = 3; break;
                case "missing-schema": first.Remove("schemaVersion"); break;
                case "missing-required-field": first.Remove("errorText"); break;
                case "missing-sequence": first.Remove("sequence"); break;
                case "success-exit-code": first["exitCode"] = 0; break;
                case "different-pid": first["terminalSessionId"] = 7; break;
                case "different-generation": first["historyGeneration"] = 1; break;
                case "different-sequence": first["sequence"] = 99; break;
                case "different-count": records.Clear(); break;
                case "null-record": records[0] = null; break;
            }
        });
        var original = await File.ReadAllBytesAsync(scope.Files.HistoryPath);
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadRecordsAsync());
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 10));
        Assert.Equal(original, await File.ReadAllBytesAsync(scope.Files.HistoryPath));
    }

    [Fact]
    public async Task ConcurrentInstances_AllocateUniqueSequencesWithoutLostWrites()
    {
        using var scope = new FailureHistoryTestScope();
        await scope.Configuration.SetConfigurationAsync("agent.recovery.historySize", 20);
        var keys = new TestFailureHistoryKeys();
        var entries = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            Task.Run(() => scope.CreateStore(keys).SaveAsync(CreateRecord(), 20))));
        var records = await scope.CreateStore(keys).ReadRecordsAsync();
        Assert.Equal(20, records.Count);
        Assert.Equal(entries.Select(entry => entry.Id).Order(), records.Select(record => record.Id).Order());
        Assert.Equal(Enumerable.Range(0, 20).Reverse().Select(index => (ulong)index), records.Select(record => record.Sequence));
        Assert.Equal(1, keys.CreateRequests.Count(create => create));
    }

    [Fact]
    public async Task FailedAtomicReplacement_PreservesSnapshotAndNeverReusesTheReservation()
    {
        var fail = false;
        using var scope = new FailureHistoryTestScope(replace: (source, target) =>
        {
            if (fail && Path.GetFileName(target) == EncryptedFailureHistoryStore.HistoryFileName)
                throw new IOException("private filesystem diagnostic");
            File.Move(source, target, overwrite: true);
        });
        var keys = new TestFailureHistoryKeys();
        var store = scope.CreateStore(keys);
        var first = await store.SaveAsync(CreateRecord(), 10);
        var original = await File.ReadAllBytesAsync(scope.Files.HistoryPath);
        fail = true;
        var error = await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 10));
        Assert.Equal("failure_history_io", error.Code);
        Assert.DoesNotContain("private filesystem diagnostic", error.ToString());
        Assert.Equal(original, await File.ReadAllBytesAsync(scope.Files.HistoryPath));
        Assert.Equal(first.Id, Assert.Single(await store.ReadRecordsAsync()).Id);
        Assert.Empty(Directory.GetFiles(scope.DirectoryPath, "*.tmp"));
        fail = false;
        var next = await store.SaveAsync(CreateRecord(), 10);
        Assert.True(FailureId.TryParse(next.Id, out var parsed));
        Assert.Equal(2UL, parsed.Sequence);
        AssertKeysCleared(keys);
    }

    [Fact]
    public async Task WriterLock_HasABoundedAndCancellableWait()
    {
        using var scope = new FailureHistoryTestScope(lockTimeout: TimeSpan.FromMilliseconds(80));
        Directory.CreateDirectory(scope.DirectoryPath);
        using var held = new FileStream(Path.Combine(scope.DirectoryPath, EncryptedFailureHistoryStore.LockFileName),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var store = scope.CreateStore(NoKeyAccess());
        var watch = Stopwatch.StartNew();
        Assert.Equal("failure_history_locked",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 10))).Code);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(CreateRecord(), 10, cancellation.Token));
        Assert.False(File.Exists(scope.Files.HistoryPath));
    }

    [Fact]
    public async Task AlreadyCancelledOperations_HaveNoSideEffects()
    {
        using var scope = new FailureHistoryTestScope();
        var store = scope.CreateStore(NoKeyAccess());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(CreateRecord(), 10, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadRecordsAsync(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadIndexAsync(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadGenerationAsync(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ClearAsync(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ApplyPolicyAsync(cancellation.Token));
        Assert.False(Directory.Exists(scope.Root));
    }

    [Fact]
    public async Task CancellationAfterKeyRetrieval_StillClearsOwnedBytes()
    {
        using var scope = new FailureHistoryTestScope();
        using var cancellation = new CancellationTokenSource();
        var owned = Enumerable.Repeat((byte)0x55, 32).ToArray();
        var keys = new TestFailureHistoryKeys
        {
            Read = (_, _, _) => { cancellation.Cancel(); return Task.FromResult(owned); },
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.CreateStore(keys).SaveAsync(CreateRecord(), 10, cancellation.Token));
        Assert.All(owned, value => Assert.Equal((byte)0, value));
        Assert.False(File.Exists(scope.Files.HistoryPath));
    }

    private static TestFailureHistoryKeys NoKeyAccess() =>
        new() { Read = (_, _, _) => throw new InvalidOperationException("Unexpected key access.") };

    private static void AssertKeysCleared(TestFailureHistoryKeys keys) =>
        Assert.All(keys.ReturnedKeys, key => Assert.All(key, value => Assert.Equal((byte)0, value)));

    private static int HeaderLength(byte[] bytes) =>
        FailureHistorySnapshot.HeaderSize + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(32));

    private static async Task RewritePayloadAsync(string path, byte[] key, Action<JsonArray> change)
    {
        var original = await File.ReadAllBytesAsync(path);
        var header = HeaderLength(original);
        var cipherOffset = header + FailureHistorySnapshot.NonceSize + FailureHistorySnapshot.TagSize;
        var plaintext = new byte[original.Length - cipherOffset];
        byte[]? modified = null;
        try
        {
            using var aes = new AesGcm(key, FailureHistorySnapshot.TagSize);
            aes.Decrypt(original.AsSpan(header, FailureHistorySnapshot.NonceSize), original.AsSpan(cipherOffset),
                original.AsSpan(header + FailureHistorySnapshot.NonceSize, FailureHistorySnapshot.TagSize), plaintext, original.AsSpan(0, header));
            var records = JsonNode.Parse(plaintext)!.AsArray();
            change(records);
            modified = JsonSerializer.SerializeToUtf8Bytes(records);
            var rewritten = new byte[cipherOffset + modified.Length];
            original.AsSpan(0, header).CopyTo(rewritten);
            BinaryPrimitives.WriteInt64LittleEndian(rewritten.AsSpan(16), modified.Length);
            RandomNumberGenerator.Fill(rewritten.AsSpan(header, FailureHistorySnapshot.NonceSize));
            aes.Encrypt(rewritten.AsSpan(header, FailureHistorySnapshot.NonceSize), modified, rewritten.AsSpan(cipherOffset),
                rewritten.AsSpan(header + FailureHistorySnapshot.NonceSize, FailureHistorySnapshot.TagSize), rewritten.AsSpan(0, header));
            await File.WriteAllBytesAsync(path, rewritten);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (modified is not null)
                CryptographicOperations.ZeroMemory(modified);
        }
    }
}
