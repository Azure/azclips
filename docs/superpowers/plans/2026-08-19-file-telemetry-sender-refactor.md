# FileTelemetrySender Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split the 438-line `FileTelemetrySender` into single-purpose types so the telemetry uploader becomes readable and unit-testable.

**Architecture:** Extract six collaborators out of one class — `TraceEntry` (DTO), `TraceFileFormat` (file content), `TraceFileStore` (file naming, lifecycle, all `System.IO`), `TraceReplayer` (pure replay), `ITraceExportSink` + `TracerProviderExportSink` (the swappable send step), and `TraceUploader` (orchestration). A single static composition root, `TelemetryUpload`, hand-wires them because `--upload-telemetry` runs before the DI container exists. Each task moves one responsibility out and leaves the build green, so behavior is identical at every commit.

**Tech Stack:** C# / .NET 10, OpenTelemetry 1.15.3, Azure.Monitor.OpenTelemetry.Exporter 1.8.0, xunit 2.9.3.

**Design spec:** `docs/superpowers/specs/2026-08-19-file-telemetry-sender-refactor-design.md`

## Global Constraints

- Target framework is `net10.0`; nullable and implicit usings are enabled. Do not add `using System;`, `using System.IO;`, `using System.Collections.Generic;`, or `using System.Linq;` — they are implicit.
- Every new `.cs` file starts with exactly these two lines:
  ```csharp
  // Copyright (c) Microsoft Corporation.
  // Licensed under the MIT License.
  ```
- All new types go in namespace `Core.Telemetry`, in `src/Core/Core.Telemetry/`, one public-or-internal type per file named after the type.
- New types are `internal` unless this plan explicitly says `public`. `Core.Telemetry.csproj` already grants `InternalsVisibleTo` to `Core.Telemetry.Tests`, so tests can see internal types.
- Test files go in `tests/Core.Telemetry.Tests/`, namespace `Core.Telemetry.Tests`. `Xunit` is a global using — do **not** write `using Xunit;`.
- A test method whose **signature** references an internal type must be declared `internal`, not `public` (see the existing `TelemetryExportersTests.AddTraceExporter_BuildsProvider_WithoutThrowing`). Internal types used only inside a method body are fine in a `public` method.
- Never write a bare `catch { }`. Every catch names its expected exception types and carries a comment explaining why that failure is expected. Use the form:
  ```csharp
  catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
  ```
- Do not add logging to the upload path. It runs in a detached process whose stderr nobody reads.
- Commit messages use the repo convention `{Telemetry} Description` — see `git log --oneline`.
- `src/Directory.Build.props` sets `TreatWarningsAsErrors=true`, so any compiler warning in product code fails the build. Test projects do not. Existing code catches `Exception` and leaves redundant `using` directives in place, so neither CA1031 nor IDE0005 is enforced — where this plan says to remove a `using`, put it back if the build disagrees.
- **Test command for tasks 1–6:** `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj`. Build only this project during the loop; a full `dotnet build azclips.slnx` pulls in `Core.Agent`, which downloads an npm package and can fail on TLS. If you must do a full solution build, use the `building-the-solution` skill.

---

### Task 1: Extract `TraceEntry` with `TryGetServiceName()`

Moves the DTO out of `FileTelemetrySender` and gives it responsibility for interpreting its own `Resource` block, replacing the private `ExtractServiceName` helper.

**Files:**
- Create: `src/Core/Core.Telemetry/TraceEntry.cs`
- Create: `tests/Core.Telemetry.Tests/TraceEntryTests.cs`
- Modify: `src/Core/Core.Telemetry/FileTelemetrySender.cs` — delete nested `TraceEntry` class (lines 418–436) and `ExtractServiceName` (lines 145–162); update call site at line 97
- Modify: `tests/Core.Telemetry.Tests/FileTelemetrySenderTests.cs` — retarget `FileTelemetrySender.TraceEntry` → `TraceEntry`

**Interfaces:**
- Consumes: nothing.
- Produces: `internal sealed class Core.Telemetry.TraceEntry` with settable properties `TraceId`, `SpanId`, `ParentSpanId`, `OperationName`, `DisplayName`, `StartTime` (all `string?`), `Duration` (`double`), `Status`, `StatusDescription`, `Kind` (all `string?`), `Tags`, `Events`, `Resource` (all `JsonElement`), and instance method `public string? TryGetServiceName()`.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Telemetry.Tests/TraceEntryTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Core.Telemetry.Tests;

public class TraceEntryTests
{
    [Fact]
    public void TryGetServiceName_ReturnsName_WhenResourceCarriesIt()
    {
        var entry = new TraceEntry { Resource = ParseJson("""{ "service.name": "CLI" }""") };

        Assert.Equal("CLI", entry.TryGetServiceName());
    }

    [Fact]
    public void TryGetServiceName_ReturnsNull_WhenResourceIsNotAnObject()
    {
        var entry = new TraceEntry { Resource = ParseJson("[]") };

        Assert.Null(entry.TryGetServiceName());
    }

    [Fact]
    public void TryGetServiceName_ReturnsNull_WhenPropertyIsMissing()
    {
        var entry = new TraceEntry { Resource = ParseJson("""{ "service.version": "1.0" }""") };

        Assert.Null(entry.TryGetServiceName());
    }

    [Fact]
    public void TryGetServiceName_ReturnsNull_WhenNameIsEmpty()
    {
        var entry = new TraceEntry { Resource = ParseJson("""{ "service.name": "" }""") };

        Assert.Null(entry.TryGetServiceName());
    }

    [Fact]
    public void TryGetServiceName_ReturnsNull_WhenNameIsNotAString()
    {
        var entry = new TraceEntry { Resource = ParseJson("""{ "service.name": 42 }""") };

        Assert.Null(entry.TryGetServiceName());
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TraceEntryTests"`

Expected: FAIL to compile — `The type or namespace name 'TraceEntry' could not be found`.

- [ ] **Step 3: Create the type**

Create `src/Core/Core.Telemetry/TraceEntry.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Core.Telemetry;

/// <summary>
/// Deserialization model matching the JSON written by <see cref="FileExporter"/>.
/// </summary>
internal sealed class TraceEntry
{
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? ParentSpanId { get; set; }
    public string? OperationName { get; set; }
    public string? DisplayName { get; set; }
    public string? StartTime { get; set; }
    public double Duration { get; set; }
    public string? Status { get; set; }
    public string? StatusDescription { get; set; }
    public string? Kind { get; set; }
    public JsonElement Tags { get; set; }
    public JsonElement Events { get; set; }
    public JsonElement Resource { get; set; }

    /// <summary>Reads <c>service.name</c> from the Resource block, or null when absent.</summary>
    public string? TryGetServiceName()
    {
        if (Resource.ValueKind == JsonValueKind.Object &&
            Resource.TryGetProperty("service.name", out var nameElement) &&
            nameElement.ValueKind == JsonValueKind.String &&
            nameElement.GetString() is string name &&
            !string.IsNullOrEmpty(name))
        {
            return name;
        }

        return null;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TraceEntryTests"`

Expected: PASS, 5 tests.

- [ ] **Step 5: Remove the old copies from `FileTelemetrySender`**

In `src/Core/Core.Telemetry/FileTelemetrySender.cs`, delete the entire nested class (the `/// <summary> Deserialization model ... </summary>` comment plus `internal class TraceEntry { ... }`, lines 418–436) and the entire `ExtractServiceName` method plus its doc comment (lines 145–162).

Then change the service-name lookup inside `SendPendingStatic` from:

```csharp
                var serviceName = ExtractServiceName(traces) ?? FallbackSourceName;
```

to:

```csharp
                var serviceName = (traces.Count > 0 ? traces[0].TryGetServiceName() : null)
                    ?? FallbackSourceName;
```

- [ ] **Step 6: Retarget the existing tests**

In `tests/Core.Telemetry.Tests/FileTelemetrySenderTests.cs`, replace all **three** occurrences of `FileTelemetrySender.TraceEntry` with `TraceEntry`:

1. `Array.Empty<FileTelemetrySender.TraceEntry>()` in `ReplayEntries_ShouldDoNothing_WhenEntriesIsEmpty` (line 24)
2. the `MakeEntry` return type (line 243)
3. the `new FileTelemetrySender.TraceEntry` construction inside `MakeEntry` (line 249)

Leave the `<see cref="FileTelemetrySender"/>` in the class doc comment alone — Task 4 handles it.

- [ ] **Step 7: Run the whole suite**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj`

Expected: PASS. The 15 pre-existing replay tests plus the 5 new ones.

- [ ] **Step 8: Commit**

```bash
git add src/Core/Core.Telemetry/TraceEntry.cs tests/Core.Telemetry.Tests/TraceEntryTests.cs src/Core/Core.Telemetry/FileTelemetrySender.cs tests/Core.Telemetry.Tests/FileTelemetrySenderTests.cs
git commit -m "{Telemetry} Extract TraceEntry with TryGetServiceName"
```

---

### Task 2: Extract `TraceFileFormat`

Gives the on-disk batch format a single owner. `FileExporter` writes through it and the uploader reads through it, so the two halves can no longer drift.

**Files:**
- Create: `src/Core/Core.Telemetry/TraceFileFormat.cs`
- Create: `tests/Core.Telemetry.Tests/TraceFileFormatTests.cs`
- Modify: `src/Core/Core.Telemetry/FileExporter.cs:80-89` — serialize via `TraceFileFormat.FormatBatch`
- Modify: `src/Core/Core.Telemetry/FileTelemetrySender.cs` — delete `ParseTraceFile` (lines ~164–195 after Task 1), call `TraceFileFormat.Parse`

**Interfaces:**
- Consumes: `TraceEntry` from Task 1.
- Produces: `internal static class TraceFileFormat` with `internal static string FormatBatch(IReadOnlyList<object> traces, DateTime timestampUtc)` and `internal static List<TraceEntry> Parse(string content)`.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Telemetry.Tests/TraceFileFormatTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry.Tests;

public class TraceFileFormatTests
{
    [Fact]
    public void Parse_ReturnsEntriesFromEveryBatch()
    {
        var content = """

            --- Batch at 2024-01-01T00:00:00.0000000Z ---
            [ { "OperationName": "first" } ]

            --- Batch at 2024-01-01T00:00:01.0000000Z ---
            [ { "OperationName": "second" }, { "OperationName": "third" } ]
            """;

        var entries = TraceFileFormat.Parse(content);

        Assert.Equal(
            new[] { "first", "second", "third" },
            entries.Select(e => e.OperationName));
    }

    [Fact]
    public void Parse_SkipsMalformedBatch_AndKeepsSiblings()
    {
        var content = """

            --- Batch at 2024-01-01T00:00:00.0000000Z ---
            [ { "OperationName": "good" } ]

            --- Batch at 2024-01-01T00:00:01.0000000Z ---
            [ { "OperationName": not-json } ]
            """;

        var entries = TraceFileFormat.Parse(content);

        Assert.Equal("good", Assert.Single(entries).OperationName);
    }

    [Fact]
    public void Parse_ReturnsEmpty_WhenContentHasNoJson()
    {
        var entries = TraceFileFormat.Parse("--- Batch at 2024-01-01T00:00:00.0000000Z ---");

        Assert.Empty(entries);
    }

    [Fact]
    public void Parse_ReturnsEmpty_WhenContentIsEmpty()
    {
        Assert.Empty(TraceFileFormat.Parse(string.Empty));
    }

    [Fact]
    public void Parse_SkipsBatch_WhenBracketsAreOutOfOrder()
    {
        var entries = TraceFileFormat.Parse("--- Batch at x ---\n] [");

        Assert.Empty(entries);
    }

    [Fact]
    public void FormatBatch_RoundTripsThroughParse()
    {
        var traces = new List<object>
        {
            new
            {
                TraceId = "0123456789abcdef0123456789abcdef",
                OperationName = "round-trip",
                Duration = 12.5,
            },
        };

        var content = TraceFileFormat.FormatBatch(traces, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var entries = TraceFileFormat.Parse(content);

        var entry = Assert.Single(entries);
        Assert.Equal("0123456789abcdef0123456789abcdef", entry.TraceId);
        Assert.Equal("round-trip", entry.OperationName);
        Assert.Equal(12.5, entry.Duration);
    }

    [Fact]
    public void FormatBatch_AppendsCleanly_SoTwoBatchesBothParse()
    {
        var first = TraceFileFormat.FormatBatch(
            new List<object> { new { OperationName = "one" } },
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var second = TraceFileFormat.FormatBatch(
            new List<object> { new { OperationName = "two" } },
            new DateTime(2024, 1, 1, 0, 0, 1, DateTimeKind.Utc));

        var entries = TraceFileFormat.Parse(first + second);

        Assert.Equal(new[] { "one", "two" }, entries.Select(e => e.OperationName));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TraceFileFormatTests"`

Expected: FAIL to compile — `The name 'TraceFileFormat' does not exist`.

- [ ] **Step 3: Create the type**

Create `src/Core/Core.Telemetry/TraceFileFormat.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Core.Telemetry;

/// <summary>Reads and writes the trace file format: timestamped JSON batches.</summary>
internal static class TraceFileFormat
{
    private const string BatchHeaderPrefix = "--- Batch at ";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>Renders one batch for appending to a trace file.</summary>
    internal static string FormatBatch(IReadOnlyList<object> traces, DateTime timestampUtc)
    {
        var json = JsonSerializer.Serialize(traces, WriteOptions);
        return $"\n{BatchHeaderPrefix}{timestampUtc:O} ---\n{json}\n";
    }

    /// <summary>Parses every batch in a file. Malformed batches are skipped.</summary>
    internal static List<TraceEntry> Parse(string content)
    {
        var allTraces = new List<TraceEntry>();

        foreach (var part in content.Split(BatchHeaderPrefix))
        {
            var jsonStart = part.IndexOf('[');
            if (jsonStart < 0)
                continue;

            var jsonEnd = part.LastIndexOf(']');
            if (jsonEnd < jsonStart)
                continue;

            try
            {
                var traces = JsonSerializer.Deserialize<List<TraceEntry>>(part[jsonStart..(jsonEnd + 1)]);
                if (traces != null)
                    allTraces.AddRange(traces);
            }
            catch (JsonException)
            {
                // Truncated or corrupt batch.
            }
        }

        return allTraces;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TraceFileFormatTests"`

Expected: PASS, 7 tests.

- [ ] **Step 5: Route `FileExporter` through the format**

In `src/Core/Core.Telemetry/FileExporter.cs`, replace the write block (currently lines 80–89):

```csharp
            // Synchronously write to file
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(traces, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                // Append to file with timestamp
                File.AppendAllText(_filePath, $"\n--- Batch at {DateTime.UtcNow:O} ---\n{json}\n");
            }
```

with:

```csharp
            // Synchronously write to file
            lock (_lock)
            {
                File.AppendAllText(_filePath, TraceFileFormat.FormatBatch(traces, DateTime.UtcNow));
            }
```

Then remove `using System.Text.Json;` from the top of `FileExporter.cs` — nothing else in that file uses it once `FormatBatch` owns serialization. A redundant `using` is not a build error, so restore it if the compiler disagrees.

- [ ] **Step 6: Route `FileTelemetrySender` through the format**

In `src/Core/Core.Telemetry/FileTelemetrySender.cs`, delete the entire `ParseTraceFile` method and its doc comment, and change the call site inside `SendPendingStatic` from:

```csharp
                var traces = ParseTraceFile(content);
```

to:

```csharp
                var traces = TraceFileFormat.Parse(content);
```

- [ ] **Step 7: Run the whole suite**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj`

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/Core/Core.Telemetry/TraceFileFormat.cs tests/Core.Telemetry.Tests/TraceFileFormatTests.cs src/Core/Core.Telemetry/FileExporter.cs src/Core/Core.Telemetry/FileTelemetrySender.cs
git commit -m "{Telemetry} Give the trace file format a single owner"
```

---

### Task 3: Extract `TraceFileStore`

Moves the directory, the file naming convention, and the claim protocol into one type. After this task it is the only type in the upload pipeline that touches `System.IO`.

**Files:**
- Create: `src/Core/Core.Telemetry/TraceFileStore.cs`
- Create: `tests/Core.Telemetry.Tests/TraceFileStoreTests.cs`
- Modify: `src/Core/Core.Telemetry/FileExporter.cs:37` — name files via `TraceFileStore.FileNameFor`
- Modify: `src/Core/Core.Telemetry/FileTelemetrySender.cs` — delete `GetTraceDirectory`, `TracesSubdirectoryName`, the stale-reclaim loop, the claim loop, and the delete loop; use the store instead. Keep `GetTraceDirectory` as a wrapper for now — call sites move in Task 7.

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `internal sealed class TraceFileStore` with constructor `TraceFileStore(string directory)`, statics `internal static string DefaultDirectory { get; }` and `internal static string FileNameFor(string traceId)`, and instance methods `internal IReadOnlyList<string> ClaimPending()`, `internal bool TryRead(string claimedFile, out string content)`, `internal void Delete(string claimedFile)`.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Telemetry.Tests/TraceFileStoreTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry.Tests;

public class TraceFileStoreTests
{
    [Fact]
    public void FileNameFor_UsesTracesPrefixAndLogExtension()
    {
        Assert.Equal("traces-abc123.log", TraceFileStore.FileNameFor("abc123"));
    }

    [Fact]
    public void ClaimPending_ReturnsEmpty_WhenDirectoryDoesNotExist()
    {
        var missing = Path.Combine(Path.GetTempPath(), "azclips-missing-" + Guid.NewGuid().ToString("N"));

        var store = new TraceFileStore(missing);

        Assert.Empty(store.ClaimPending());
    }

    [Fact]
    public void ClaimPending_ReturnsEmpty_WhenDirectoryHasNoTraceFiles()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "unrelated.txt"), "data");

        var store = new TraceFileStore(temp.Path);

        Assert.Empty(store.ClaimPending());
    }

    [Fact]
    public void ClaimPending_RenamesPendingFilesToUploading_AndReturnsThem()
    {
        using var temp = new TempDirectory();
        var pending = WritePendingFile(temp.Path, "aaa", "payload");
        var store = new TraceFileStore(temp.Path);

        var claimed = store.ClaimPending();

        var claimedPath = Assert.Single(claimed);
        Assert.Equal(".uploading", Path.GetExtension(claimedPath));
        Assert.True(File.Exists(claimedPath));
        Assert.False(File.Exists(pending));
    }

    [Fact]
    public void ClaimPending_IgnoresFilesAlreadyClaimedByAnotherUploader()
    {
        using var temp = new TempDirectory();
        // A fresh .uploading file belongs to another uploader.
        File.WriteAllText(Path.Combine(temp.Path, "traces-bbb.uploading"), "payload");
        var store = new TraceFileStore(temp.Path);

        Assert.Empty(store.ClaimPending());
    }

    [Fact]
    public void ClaimPending_ReclaimsClaimsAbandonedByACrashedUploader()
    {
        using var temp = new TempDirectory();
        var stale = Path.Combine(temp.Path, "traces-ccc.uploading");
        File.WriteAllText(stale, "payload");
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddMinutes(-10));
        var store = new TraceFileStore(temp.Path);

        var claimed = store.ClaimPending();

        // The sweep renames it back to .log, then this caller claims it.
        var claimedPath = Assert.Single(claimed);
        Assert.Equal("traces-ccc.uploading", Path.GetFileName(claimedPath));
        Assert.True(File.Exists(claimedPath));
    }

    [Fact]
    public void TryRead_ReturnsContentOfClaimedFile()
    {
        using var temp = new TempDirectory();
        WritePendingFile(temp.Path, "ddd", "payload");
        var store = new TraceFileStore(temp.Path);
        var claimedPath = Assert.Single(store.ClaimPending());

        Assert.True(store.TryRead(claimedPath, out var content));
        Assert.Equal("payload", content);
    }

    [Fact]
    public void TryRead_ReturnsFalse_WhenFileIsMissing()
    {
        using var temp = new TempDirectory();
        var store = new TraceFileStore(temp.Path);

        Assert.False(store.TryRead(Path.Combine(temp.Path, "traces-eee.uploading"), out var content));
        Assert.Equal(string.Empty, content);
    }

    [Fact]
    public void Delete_RemovesTheClaimedFile()
    {
        using var temp = new TempDirectory();
        WritePendingFile(temp.Path, "fff", "payload");
        var store = new TraceFileStore(temp.Path);
        var claimedPath = Assert.Single(store.ClaimPending());

        store.Delete(claimedPath);

        Assert.False(File.Exists(claimedPath));
    }

    [Fact]
    public void Delete_DoesNotThrow_WhenFileIsAlreadyGone()
    {
        using var temp = new TempDirectory();
        var store = new TraceFileStore(temp.Path);

        Assert.Null(Record.Exception(
            () => store.Delete(Path.Combine(temp.Path, "traces-ggg.uploading"))));
    }

    private static string WritePendingFile(string directory, string traceId, string content)
    {
        var path = Path.Combine(directory, TraceFileStore.FileNameFor(traceId));
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>A throwaway directory removed when the test finishes.</summary>
    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "azclips-trace-store-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Cleanup only.
            }
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TraceFileStoreTests"`

Expected: FAIL to compile — `The type or namespace name 'TraceFileStore' could not be found`.

- [ ] **Step 3: Create the type**

Create `src/Core/Core.Telemetry/TraceFileStore.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// Owns the trace directory, trace file naming, and the rename-based claim protocol that
/// lets concurrent uploaders share the directory.
/// </summary>
internal sealed class TraceFileStore
{
    private const string TracesSubdirectoryName = "traces";
    private const string FilePrefix = "traces-";
    private const string PendingExtension = ".log";
    private const string ClaimedExtension = ".uploading";

    /// <summary>A claim older than this is treated as abandoned by a crashed uploader.</summary>
    private static readonly TimeSpan StaleClaimAge = TimeSpan.FromMinutes(5);

    private readonly string _directory;

    internal TraceFileStore(string directory)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
    }

    /// <summary>The shared trace directory under <c>{LocalApplicationData}/.azclips/</c>.</summary>
    internal static string DefaultDirectory =>
        Path.Combine(HomePaths.GetDefaultLocalAppDataRoot(), TracesSubdirectoryName);

    /// <summary>The file name a given trace buffers into.</summary>
    internal static string FileNameFor(string traceId) =>
        $"{FilePrefix}{traceId}{PendingExtension}";

    /// <summary>
    /// Reclaims abandoned claims, then takes ownership of pending files by renaming them.
    /// Returns only the files this caller won.
    /// </summary>
    internal IReadOnlyList<string> ClaimPending()
    {
        if (!Directory.Exists(_directory))
            return Array.Empty<string>();

        ReclaimStaleClaims();

        var claimed = new List<string>();

        foreach (var pending in Directory.GetFiles(_directory, FilePrefix + "*" + PendingExtension))
        {
            var target = Path.ChangeExtension(pending, ClaimedExtension);
            try
            {
                File.Move(pending, target);
                claimed.Add(target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Another uploader won the file, or a writer still holds it.
            }
        }

        return claimed;
    }

    /// <summary>Reads a claimed file. Returns false when it cannot be read.</summary>
    internal bool TryRead(string claimedFile, out string content)
    {
        try
        {
            content = File.ReadAllText(claimedFile);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Vanished or locked.
            content = string.Empty;
            return false;
        }
    }

    /// <summary>Removes a claimed file.</summary>
    internal void Delete(string claimedFile)
    {
        try
        {
            File.Delete(claimedFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Already gone or locked.
        }
    }

    private void ReclaimStaleClaims()
    {
        var cutoff = DateTime.UtcNow - StaleClaimAge;

        foreach (var stale in Directory.GetFiles(_directory, FilePrefix + "*" + ClaimedExtension))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(stale) < cutoff)
                    File.Move(stale, Path.ChangeExtension(stale, PendingExtension));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Another uploader reclaimed it first.
            }
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TraceFileStoreTests"`

Expected: PASS, 10 tests.

- [ ] **Step 5: Route `FileExporter` through the store's naming**

In `src/Core/Core.Telemetry/FileExporter.cs`, change line 37 from:

```csharp
                    _filePath = Path.Combine(_rootDirectory, $"traces-{traceId}.log");
```

to:

```csharp
                    _filePath = Path.Combine(_rootDirectory, TraceFileStore.FileNameFor(traceId));
```

- [ ] **Step 6: Route `FileTelemetrySender` through the store**

In `src/Core/Core.Telemetry/FileTelemetrySender.cs`:

Delete the `TracesSubdirectoryName` constant and change `GetTraceDirectory` to delegate (call sites move in Task 7):

```csharp
    /// <summary>The shared trace directory. Removed in Task 6.</summary>
    public static string GetTraceDirectory() => TraceFileStore.DefaultDirectory;
```

Then replace the opening of `SendPendingStatic` — the existence check, the stale-reclaim loop, the `Directory.GetFiles` call, and the claim loop (everything from `var traceDir = GetTraceDirectory();` down to and including the `if (claimedFiles.Count == 0) return Task.CompletedTask;` block) — with:

```csharp
        var store = new TraceFileStore(TraceFileStore.DefaultDirectory);

        var claimedFiles = store.ClaimPending();
        if (claimedFiles.Count == 0)
            return Task.CompletedTask;
```

In the per-file loop, replace the `File.ReadAllText` call and its surrounding `try`/`catch` with the store's `TryRead`:

```csharp
        foreach (var file in claimedFiles)
        {
            if (!store.TryRead(file, out var content))
                continue;

            var traces = TraceFileFormat.Parse(content);
            var serviceName = (traces.Count > 0 ? traces[0].TryGetServiceName() : null)
                ?? FallbackSourceName;

            if (!tracesByService.ContainsKey(serviceName))
                tracesByService[serviceName] = new();
            tracesByService[serviceName].Add((file, traces));
        }
```

Finally replace the delete loop at the end:

```csharp
        foreach (var file in replayedFiles)
        {
            try { File.Delete(file); } catch { }
        }
```

with:

```csharp
        foreach (var file in replayedFiles)
        {
            store.Delete(file);
        }
```

Leave the rest of `SendPendingStatic` untouched — Tasks 4 through 6 dismantle it.

- [ ] **Step 7: Run the whole suite**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj`

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/Core/Core.Telemetry/TraceFileStore.cs tests/Core.Telemetry.Tests/TraceFileStoreTests.cs src/Core/Core.Telemetry/FileExporter.cs src/Core/Core.Telemetry/FileTelemetrySender.cs
git commit -m "{Telemetry} Extract TraceFileStore for trace file lifecycle"
```

---

### Task 4: Extract `TraceReplayer`

A pure move of the replay engine — the largest and riskiest block — protected by the 15 tests that already exist for it.

**Files:**
- Create: `src/Core/Core.Telemetry/TraceReplayer.cs`
- Modify: `src/Core/Core.Telemetry/FileTelemetrySender.cs` — delete `ReplayEntries`, `TopologicallyOrder`, `ReplayTrace`; call `TraceReplayer.ReplayEntries`
- Rename: `tests/Core.Telemetry.Tests/FileTelemetrySenderTests.cs` → `tests/Core.Telemetry.Tests/TraceReplayerTests.cs`

**Interfaces:**
- Consumes: `TraceEntry` from Task 1.
- Produces: `internal static class TraceReplayer` with `internal static void ReplayEntries(ActivitySource source, IEnumerable<TraceEntry> entries)`.

- [ ] **Step 1: Move the test file first, so it fails**

```bash
git mv tests/Core.Telemetry.Tests/FileTelemetrySenderTests.cs tests/Core.Telemetry.Tests/TraceReplayerTests.cs
```

In the moved file, rename the class `FileTelemetrySenderTests` → `TraceReplayerTests`, update the class doc comment to reference `<see cref="TraceReplayer"/>`, and replace all 15 occurrences of `FileTelemetrySender.ReplayEntries(` with `TraceReplayer.ReplayEntries(`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TraceReplayerTests"`

Expected: FAIL to compile — `The name 'TraceReplayer' does not exist`.

- [ ] **Step 3: Create the type**

Create `src/Core/Core.Telemetry/TraceReplayer.cs`. Move the **method bodies verbatim** out of `FileTelemetrySender.cs` — do not rewrite the logic. The 15 existing test methods must keep passing without modification, so any behavior change here defeats the task. Comments are condensed below; code is not.

In the pre-refactor file these were lines 197–416 (`ReplayEntries`, `TopologicallyOrder`, and `ReplayTrace`, each with its doc comment). If earlier tasks shifted the numbering, locate them by name.

Wrap them in this file:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;

namespace Core.Telemetry;

/// <summary>
/// Replays trace entries through an <see cref="ActivitySource"/>, preserving the original
/// TraceId and parent/child hierarchy.
/// </summary>
internal static class TraceReplayer
{
    // <-- ReplayEntries, TopologicallyOrder and ReplayTrace pasted here.
}
```

Accessibility does not change: `ReplayEntries` stays `internal static`, and `TopologicallyOrder` and `ReplayTrace` stay `private static`.

Replace the three method doc comments with these shorter versions. The method **bodies** stay byte-for-byte identical — the tests verify behavior, not comments:

```csharp
    /// <summary>
    /// Replays entries parent-before-child, remapping each child's parent context to the
    /// SpanId its parent was assigned during replay.
    /// </summary>
    internal static void ReplayEntries(...)

    /// <summary>
    /// Orders entries so every parent precedes its children. Unparented entries are roots;
    /// entries left over from cycles are appended at the end.
    /// </summary>
    private static List<TraceEntry> TopologicallyOrder(...)

    /// <summary>
    /// Reconstructs one Activity from an entry and records its new SpanId in
    /// <paramref name="spanIdMap"/> for its descendants.
    /// </summary>
    private static void ReplayTrace(...)
```

And condense these inline comments inside the moved bodies:

| Replace | With |
| --- | --- |
| the 3-line `// Maps each entry's original SpanId to the new SpanId assigned when its replay activity was started…` | `// Original SpanId -> the new SpanId assigned during replay.` |
| the 2-line `// Sweep up any entries left out by cycles so we never silently drop telemetry…` | `// Entries left out by cycles, appended as best-effort roots.` |
| the 6-line numbered `// Resolve the parent SpanId: 1. … 2. …` block | `// Use the parent's new SpanId when the parent was replayed in this batch; otherwise a random one, since an all-zero SpanId is invalid and would lose the TraceId.` |
| `// Parse start time before creating the activity so we can pass it to StartActivity directly.` | *(delete)* |
| the 2-line `// Record the new SpanId before the activity is disposed…` | `// Recorded before disposal so descendants can reference it.` |

Leave `// Restore tags`, `// Restore events`, `// Restore status`, and `// Restore end time (start + duration)` as they are.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TraceReplayerTests"`

Expected: PASS, 20 test cases (14 `[Fact]` methods plus one `[Theory]` with 6 `[InlineData]` rows).

- [ ] **Step 5: Delete the originals and call the new type**

In `src/Core/Core.Telemetry/FileTelemetrySender.cs`, delete `ReplayEntries`, `TopologicallyOrder`, and `ReplayTrace` entirely, then change the call inside `SendPendingStatic` from:

```csharp
            ReplayEntries(source, allEntries);
```

to:

```csharp
            TraceReplayer.ReplayEntries(source, allEntries);
```

Remove `using System.Text.Json;` from `FileTelemetrySender.cs` if nothing else there uses it.

- [ ] **Step 6: Run the whole suite**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj`

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add -A src/Core/Core.Telemetry tests/Core.Telemetry.Tests
git commit -m "{Telemetry} Extract TraceReplayer from FileTelemetrySender"
```

---

### Task 5: Extract `ITraceExportSink` and `TracerProviderExportSink`

Isolates the one step that talks to Azure Monitor, which is what makes Task 6's uploader testable.

**Files:**
- Create: `src/Core/Core.Telemetry/ITraceExportSink.cs`
- Create: `src/Core/Core.Telemetry/TracerProviderExportSink.cs`
- Create: `tests/Core.Telemetry.Tests/TracerProviderExportSinkTests.cs`
- Modify: `src/Core/Core.Telemetry/FileTelemetrySender.cs` — replace the provider-building loop body with a sink call

**Interfaces:**
- Consumes: `TraceEntry` (Task 1), `TraceReplayer.ReplayEntries` (Task 4).
- Produces: `internal interface ITraceExportSink` with `bool TryExport(string serviceName, IReadOnlyList<TraceEntry> entries)`; `internal sealed class TracerProviderExportSink : ITraceExportSink` with constructor `TracerProviderExportSink(TelemetryTarget target)`.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Telemetry.Tests/TracerProviderExportSinkTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry.Tests;

public class TracerProviderExportSinkTests
{
    [Theory]
    [InlineData(TelemetryTarget.Local)]
    [InlineData(TelemetryTarget.Remote)]
    internal void Constructor_CreatesSink_ForEachTarget(TelemetryTarget target)
    {
        // Construction only. Exercising TryExport here would build a live provider and
        // block on ForceFlush; TraceUploaderTests covers the behavior through a fake.
        ITraceExportSink sink = new TracerProviderExportSink(target);

        Assert.NotNull(sink);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TracerProviderExportSinkTests"`

Expected: FAIL to compile — `The type or namespace name 'TracerProviderExportSink' could not be found`.

- [ ] **Step 3: Create the interface**

Create `src/Core/Core.Telemetry/ITraceExportSink.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry;

/// <summary>Ships replayed traces for one service.</summary>
internal interface ITraceExportSink
{
    /// <summary>
    /// Exports one service's traces. Takes a single service per call because a
    /// TracerProvider's Resource is immutable and supplies <c>service.name</c>.
    /// Returns true only when the batch flushed successfully.
    /// </summary>
    bool TryExport(string serviceName, IReadOnlyList<TraceEntry> entries);
}
```

- [ ] **Step 4: Create the implementation**

Create `src/Core/Core.Telemetry/TracerProviderExportSink.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>Exports through a TracerProvider built for the given telemetry target.</summary>
internal sealed class TracerProviderExportSink : ITraceExportSink
{
    private const int FlushTimeoutMilliseconds = 10_000;

    private readonly TelemetryTarget _target;

    internal TracerProviderExportSink(TelemetryTarget target)
    {
        _target = target;
    }

    public bool TryExport(string serviceName, IReadOnlyList<TraceEntry> entries)
    {
        using var source = new ActivitySource(serviceName);

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(serviceName)
            .ConfigureResource(r => r.AddService(serviceName: serviceName));
        TelemetryExporters.AddTraceExporter(builder, _target);

        using var provider = builder.Build()!;

        TraceReplayer.ReplayEntries(source, entries);

        return provider.ForceFlush(FlushTimeoutMilliseconds);
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TracerProviderExportSinkTests"`

Expected: PASS, 2 test cases.

- [ ] **Step 6: Route `FileTelemetrySender` through the sink**

In `src/Core/Core.Telemetry/FileTelemetrySender.cs`, replace the whole export loop:

```csharp
        foreach (var (serviceName, fileGroup) in tracesByService)
        {
            using var source = new ActivitySource(serviceName);
            var builder = Sdk.CreateTracerProviderBuilder()
                .AddSource(serviceName)
                .ConfigureResource(r => r.AddService(serviceName: serviceName));
            TelemetryExporters.AddTraceExporter(builder, target);
            using var provider = builder.Build()!;

            var allEntries = fileGroup.SelectMany(f => f.traces);
            TraceReplayer.ReplayEntries(source, allEntries);

            if (provider.ForceFlush(timeoutMilliseconds: 10_000))
            {
                foreach (var (file, _) in fileGroup)
                    replayedFiles.Add(file);
            }
        }
```

with:

```csharp
        var sink = new TracerProviderExportSink(target);

        foreach (var (serviceName, fileGroup) in tracesByService)
        {
            // Flatten across every file in the group so the topological replay can resolve
            // parent/child links even when they landed in different files.
            var allEntries = fileGroup.SelectMany(f => f.traces).ToList();

            if (sink.TryExport(serviceName, allEntries))
            {
                foreach (var (file, _) in fileGroup)
                    replayedFiles.Add(file);
            }
        }
```

Remove `using` directives in `FileTelemetrySender.cs` that the compiler now reports as unused. `ConfigureExporter` still needs `OpenTelemetry` and `OpenTelemetry.Trace`; leaving a redundant `using` in place is not a build error, so when in doubt keep it.

- [ ] **Step 7: Run the whole suite**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj`

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/Core/Core.Telemetry/ITraceExportSink.cs src/Core/Core.Telemetry/TracerProviderExportSink.cs tests/Core.Telemetry.Tests/TracerProviderExportSinkTests.cs src/Core/Core.Telemetry/FileTelemetrySender.cs
git commit -m "{Telemetry} Put trace export behind a swappable sink"
```

---

### Task 6: Extract `TraceUploader` and `TelemetryUpload`

The payoff task. What is left of `SendPendingStatic` becomes an instance class with injected collaborators, and gains the test coverage that was impossible before.

**Files:**
- Create: `src/Core/Core.Telemetry/TraceUploader.cs`
- Create: `src/Core/Core.Telemetry/TelemetryUpload.cs`
- Create: `tests/Core.Telemetry.Tests/TraceUploaderTests.cs`
- Modify: `src/Core/Core.Telemetry/FileTelemetrySender.cs` — delete `SendPendingStatic` and `FallbackSourceName`; repoint the explicit `ITelemetrySender.SendPendingAsync` at `TelemetryUpload.RunPending()`, and keep `GetTraceDirectory` as a temporary delegating wrapper, so the class still compiles until Task 7
- Modify: `src/CLI/Program.cs:45` — call `TelemetryUpload.RunPending()`

**Interfaces:**
- Consumes: `TraceFileStore` (Task 3), `TraceFileFormat.Parse` (Task 2), `TraceEntry.TryGetServiceName` (Task 1), `ITraceExportSink` and `TracerProviderExportSink` (Task 5), `TelemetryTargetResolver.Resolve()` (existing).
- Produces: `internal sealed class TraceUploader` with constructor `TraceUploader(TraceFileStore store, ITraceExportSink sink)` and `internal void Run()`; `public static class TelemetryUpload` with `public static void RunPending()`.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Telemetry.Tests/TraceUploaderTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry.Tests;

public class TraceUploaderTests
{
    [Fact]
    public void Run_DeletesFiles_WhenExportSucceeds()
    {
        using var temp = new TempDirectory();
        WriteTraceFile(temp.Path, "aaa", serviceName: "CLI", "op-1");
        var sink = new FakeSink(exportSucceeds: true);

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        Assert.Empty(Directory.GetFiles(temp.Path));
    }

    [Fact]
    public void Run_LeavesFilesOnDisk_WhenExportFails()
    {
        using var temp = new TempDirectory();
        WriteTraceFile(temp.Path, "aaa", serviceName: "CLI", "op-1");
        var sink = new FakeSink(exportSucceeds: false);

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        Assert.Single(Directory.GetFiles(temp.Path, "traces-*.uploading"));
    }

    [Fact]
    public void Run_CallsSinkOncePerServiceName()
    {
        using var temp = new TempDirectory();
        WriteTraceFile(temp.Path, "aaa", serviceName: "CLI", "op-1");
        WriteTraceFile(temp.Path, "bbb", serviceName: "PowerShell", "op-2");
        WriteTraceFile(temp.Path, "ccc", serviceName: "CLI", "op-3");
        var sink = new FakeSink(exportSucceeds: true);

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        Assert.Equal(
            new[] { "CLI", "PowerShell" },
            sink.Calls.Select(c => c.ServiceName).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Run_FlattensEntriesAcrossFilesInTheSameGroup()
    {
        using var temp = new TempDirectory();
        WriteTraceFile(temp.Path, "aaa", serviceName: "CLI", "op-1");
        WriteTraceFile(temp.Path, "bbb", serviceName: "CLI", "op-2", "op-3");
        var sink = new FakeSink(exportSucceeds: true);

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        var call = Assert.Single(sink.Calls);
        Assert.Equal(
            new[] { "op-1", "op-2", "op-3" },
            call.Entries.Select(e => e.OperationName).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Run_UsesFallbackServiceName_WhenResourceHasNone()
    {
        using var temp = new TempDirectory();
        WriteTraceFile(temp.Path, "aaa", serviceName: null, "op-1");
        var sink = new FakeSink(exportSucceeds: true);

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        Assert.Equal("AzclipsTelemetryUploader", Assert.Single(sink.Calls).ServiceName);
    }

    [Fact]
    public void Run_DoesNothing_WhenDirectoryIsEmpty()
    {
        using var temp = new TempDirectory();
        var sink = new FakeSink(exportSucceeds: true);

        new TraceUploader(new TraceFileStore(temp.Path), sink).Run();

        Assert.Empty(sink.Calls);
    }

    private static void WriteTraceFile(
        string directory,
        string traceId,
        string? serviceName,
        params string[] operationNames)
    {
        var resource = serviceName == null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["service.name"] = serviceName };

        var traces = operationNames
            .Select(name => (object)new
            {
                TraceId = "0123456789abcdef0123456789abcdef",
                SpanId = "1111111111111111",
                ParentSpanId = "0000000000000000",
                OperationName = name,
                DisplayName = name,
                StartTime = "2024-01-01T00:00:00.0000000Z",
                Duration = 1.0,
                Status = "Unset",
                Kind = "Internal",
                Tags = Array.Empty<object>(),
                Events = Array.Empty<object>(),
                Resource = resource,
            })
            .ToList();

        File.WriteAllText(
            Path.Combine(directory, TraceFileStore.FileNameFor(traceId)),
            TraceFileFormat.FormatBatch(traces, DateTime.UtcNow));
    }

    /// <summary>Records what the uploader tried to send and returns a fixed outcome.</summary>
    private sealed class FakeSink : ITraceExportSink
    {
        private readonly bool _exportSucceeds;

        public FakeSink(bool exportSucceeds)
        {
            _exportSucceeds = exportSucceeds;
        }

        public List<(string ServiceName, IReadOnlyList<TraceEntry> Entries)> Calls { get; } = new();

        public bool TryExport(string serviceName, IReadOnlyList<TraceEntry> entries)
        {
            Calls.Add((serviceName, entries));
            return _exportSucceeds;
        }
    }

    /// <summary>A throwaway directory removed when the test finishes.</summary>
    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "azclips-trace-uploader-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Cleanup only.
            }
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TraceUploaderTests"`

Expected: FAIL to compile — `The type or namespace name 'TraceUploader' could not be found`.

- [ ] **Step 3: Create the uploader**

Create `src/Core/Core.Telemetry/TraceUploader.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry;

/// <summary>
/// Replays buffered trace files to the telemetry target and deletes the ones that were
/// delivered.
/// </summary>
internal sealed class TraceUploader
{
    private const string FallbackServiceName = "AzclipsTelemetryUploader";

    private readonly TraceFileStore _store;
    private readonly ITraceExportSink _sink;

    internal TraceUploader(TraceFileStore store, ITraceExportSink sink)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    internal void Run()
    {
        var claimedFiles = _store.ClaimPending();
        if (claimedFiles.Count == 0)
            return;

        // Group by service name so each group exports under the correct cloud_RoleName.
        // Entries are flattened across a group's files so replay can link a parent and
        // child that landed in different files.
        var groups = new Dictionary<string, (List<string> Files, List<TraceEntry> Entries)>(
            StringComparer.Ordinal);

        foreach (var file in claimedFiles)
        {
            if (!_store.TryRead(file, out var content))
                continue;

            var entries = TraceFileFormat.Parse(content);
            var serviceName = (entries.Count > 0 ? entries[0].TryGetServiceName() : null)
                ?? FallbackServiceName;

            if (!groups.TryGetValue(serviceName, out var group))
            {
                group = (new List<string>(), new List<TraceEntry>());
                groups[serviceName] = group;
            }

            group.Files.Add(file);
            group.Entries.AddRange(entries);
        }

        foreach (var (serviceName, group) in groups)
        {
            // Delete only what shipped; the rest is retried next run.
            if (!_sink.TryExport(serviceName, group.Entries))
                continue;

            foreach (var file in group.Files)
                _store.Delete(file);
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~TraceUploaderTests"`

Expected: PASS, 6 tests.

- [ ] **Step 5: Create the composition root**

Create `src/Core/Core.Telemetry/TelemetryUpload.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry;

/// <summary>
/// Entry point for the detached <c>--upload-telemetry</c> process. Runs before the DI
/// container exists, so it wires the uploader by hand.
/// </summary>
public static class TelemetryUpload
{
    /// <summary>Sends all pending trace files, then deletes the ones that were delivered.</summary>
    public static void RunPending() =>
        new TraceUploader(
            new TraceFileStore(TraceFileStore.DefaultDirectory),
            new TracerProviderExportSink(TelemetryTargetResolver.Resolve()))
        .Run();
}
```

- [ ] **Step 6: Delete the old uploader and update the CLI**

In `src/Core/Core.Telemetry/FileTelemetrySender.cs`, delete `SendPendingStatic` and its doc comment, and the `FallbackSourceName` constant.

**Keep two members for now** so this commit builds; Task 7 removes both along with their reasons for existing.

`GetTraceDirectory` still has callers in `CLITelemetryProvider` and `TUITelemetryProvider`, which Task 7 updates:

```csharp
    /// <summary>The shared trace directory. Temporary: Task 7 removes this.</summary>
    public static string GetTraceDirectory() => TraceFileStore.DefaultDirectory;
```

And `ITelemetrySender` still declares `SendPendingAsync`, so the class must still implement it:

```csharp
    // Temporary: ITelemetrySender still declares this member. Task 7 removes both.
    Task ITelemetrySender.SendPendingAsync()
    {
        TelemetryUpload.RunPending();
        return Task.CompletedTask;
    }
```

Do **not** add a directory passthrough to `TelemetryUpload` — `TraceFileStore` stays `internal`, and Task 7 removes the need for any caller to name a directory at all.

In `src/CLI/Program.cs`, change:

```csharp
        if (args.Length > 0 && args[0] == "--upload-telemetry")
        {
            await FileTelemetrySender.SendPendingStatic();
            return 0;
        }
```

to:

```csharp
        if (args.Length > 0 && args[0] == "--upload-telemetry")
        {
            TelemetryUpload.RunPending();
            return 0;
        }
```

- [ ] **Step 7: Verify both projects build and the suite passes**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj`
Then: `dotnet build src/CLI/Core.Cli.csproj`
Then: `dotnet build TUI/Core.Tui.csproj`

Expected: tests PASS; both projects build with no errors. The TUI build matters here — it is the second caller of `GetTraceDirectory`, and building only the CLI would hide a break.

- [ ] **Step 8: Commit**

```bash
git add src/Core/Core.Telemetry/TraceUploader.cs src/Core/Core.Telemetry/TelemetryUpload.cs tests/Core.Telemetry.Tests/TraceUploaderTests.cs src/Core/Core.Telemetry/FileTelemetrySender.cs src/CLI/Program.cs
git commit -m "{Telemetry} Extract TraceUploader with a hand-wired composition root"
```

---

### Task 7: Shrink `ITelemetrySender` to one method

Removes the dead `SendPendingAsync` and the `traceDirectory` parameter that is never a meaningful value at any call site.

**Files:**
- Modify: `src/Core/Core.Telemetry/ITelemetrySender.cs`
- Modify: `src/Core/Core.Telemetry/DirectTelemetrySender.cs`
- Modify: `src/Core/Core.Telemetry/FileTelemetrySender.cs`
- Modify: `src/CLI/Telemetry/CLITelemetryProvider.cs:45`
- Modify: `TUI/Telemetry/TUITelemetryProvider.cs:40`
- Modify: `src/PS/Login/Telemetry/PSTelemetryProvider.cs:43`
- Modify: `tests/Core.Telemetry.Tests/DirectTelemetrySenderTests.cs:17`

**Interfaces:**
- Consumes: `TraceFileStore.DefaultDirectory` (Task 3).
- Produces: `public interface ITelemetrySender` with the single member `void ConfigureExporter(TracerProviderBuilder builder)`.

- [ ] **Step 1: Update the existing test to the new signature**

In `tests/Core.Telemetry.Tests/DirectTelemetrySenderTests.cs`, change line 17 from:

```csharp
        sender.ConfigureExporter(builder, traceDirectory: string.Empty);
```

to:

```csharp
        sender.ConfigureExporter(builder);
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~DirectTelemetrySenderTests"`

Expected: FAIL to compile — `No overload for method 'ConfigureExporter' takes 1 arguments`.

- [ ] **Step 3: Shrink the interface**

Replace the body of `src/Core/Core.Telemetry/ITelemetrySender.cs` with:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>Abstracts how a client exports telemetry: buffered to file, or inline.</summary>
public interface ITelemetrySender
{
    /// <summary>Adds the appropriate exporter(s) to the TracerProvider being built.</summary>
    void ConfigureExporter(TracerProviderBuilder builder);
}
```

- [ ] **Step 4: Update both implementations**

Replace the body of `src/Core/Core.Telemetry/DirectTelemetrySender.cs` with:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Exports traces inline via the TracerProvider pipeline. No file buffering or replay.
/// </summary>
public class DirectTelemetrySender : ITelemetrySender
{
    public void ConfigureExporter(TracerProviderBuilder builder)
    {
        TelemetryExporters.AddTraceExporter(builder, TelemetryTargetResolver.Resolve());
    }
}
```

Replace the body of `src/Core/Core.Telemetry/FileTelemetrySender.cs` with:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Buffers traces to local files. A detached process replays them via
/// <see cref="TelemetryUpload"/>.
/// </summary>
public class FileTelemetrySender : ITelemetrySender
{
    public void ConfigureExporter(TracerProviderBuilder builder)
    {
        builder.AddProcessor(
            new SimpleActivityExportProcessor(
                new FileExporter(TraceFileStore.DefaultDirectory)));
    }
}
```

- [ ] **Step 5: Update the three call sites**

In `src/CLI/Telemetry/CLITelemetryProvider.cs`, change:

```csharp
        sender.ConfigureExporter(builder, FileTelemetrySender.GetTraceDirectory());
```

to:

```csharp
        sender.ConfigureExporter(builder);
```

In `TUI/Telemetry/TUITelemetryProvider.cs`, apply the identical change.

In `src/PS/Login/Telemetry/PSTelemetryProvider.cs`, change:

```csharp
        sender.ConfigureExporter(builder, string.Empty);
```

to:

```csharp
        sender.ConfigureExporter(builder);
```

- [ ] **Step 6: Run the tests and build every affected project**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj`
Then: `dotnet build src/CLI/Core.Cli.csproj`
Then: `dotnet build TUI/Core.Tui.csproj`
Then: `dotnet build src/PS/Login/Cmdlets.Login.csproj`

Expected: tests PASS; all three projects build with no errors.

- [ ] **Step 7: Commit**

```bash
git add src/Core/Core.Telemetry/ITelemetrySender.cs src/Core/Core.Telemetry/DirectTelemetrySender.cs src/Core/Core.Telemetry/FileTelemetrySender.cs src/CLI/Telemetry/CLITelemetryProvider.cs TUI/Telemetry/TUITelemetryProvider.cs src/PS/Login/Telemetry/PSTelemetryProvider.cs tests/Core.Telemetry.Tests/DirectTelemetrySenderTests.cs
git commit -m "{Telemetry} Reduce ITelemetrySender to a single method"
```

---

### Task 8: Update the telemetry wiki

`wiki/02-deep-dive/telemetry.md` documents this subsystem and cites `FileTelemetrySender.cs` by line number in roughly eight places. Every one of those is now wrong.

**Files:**
- Modify: `wiki/02-deep-dive/telemetry.md`

**Interfaces:**
- Consumes: the final type names from Tasks 1–7.
- Produces: nothing.

- [ ] **Step 1: Find every stale reference**

Run: `git --no-pager grep -n "FileTelemetrySender\|SendPendingStatic\|ParseTraceFile\|ReplayEntries\|GetTraceDirectory" -- wiki/`

Expected: matches in `wiki/02-deep-dive/telemetry.md`, concentrated in the "Async export path" section (~line 124), the sequence diagram (~lines 128–147), "The uploader (`SendPendingStatic`)" (~lines 155–165), and the file list (~lines 189–195).

- [ ] **Step 2: Rewrite the affected passages**

Apply these substitutions, adjusting the surrounding prose so it still reads naturally:

| Old reference | New reference |
| --- | --- |
| `FileTelemetrySender.ConfigureExporter` attaching the processor (`FileTelemetrySender.cs:32-35`) | `FileTelemetrySender.ConfigureExporter` (`src/Core/Core.Telemetry/FileTelemetrySender.cs`) — note it no longer takes a directory argument |
| trace directory at `FileTelemetrySender.cs:26-30` | `TraceFileStore.DefaultDirectory` (`src/Core/Core.Telemetry/TraceFileStore.cs`) |
| `FileTelemetrySender.SendPendingStatic()` in `Program.cs` | `TelemetryUpload.RunPending()` (`src/Core/Core.Telemetry/TelemetryUpload.cs`) |
| the section heading "The uploader (`SendPendingStatic`)" | "The uploader (`TraceUploader`)" |
| stale reclaim / atomic claim bullets citing `FileTelemetrySender.cs:43-141` | `TraceFileStore.ClaimPending` (`src/Core/Core.Telemetry/TraceFileStore.cs`) |
| "group by service" bullet | `TraceUploader.Run` (`src/Core/Core.Telemetry/TraceUploader.cs`) |
| hierarchy-preserving replay citing `FileTelemetrySender.cs:202-355` | `TraceReplayer.ReplayEntries` (`src/Core/Core.Telemetry/TraceReplayer.cs`) |
| "delete only on success" citing `FileTelemetrySender.cs:126-138` | `TracerProviderExportSink.TryExport` returning false leaves files for `TraceUploader.Run` to retry |
| file-list line `FileTelemetrySender.cs — uploader, atomic claim, hierarchy-preserving replay` | four lines: `TraceFileStore.cs — trace directory, file naming, claim protocol`; `TraceFileFormat.cs — on-disk batch format, shared by writer and reader`; `TraceReplayer.cs — hierarchy-preserving replay`; `TraceUploader.cs` / `TelemetryUpload.cs` — orchestration and composition root |

Prefer citing **file paths without line numbers** for the new references, so the document does not rot again on the next edit.

- [ ] **Step 3: Update the sequence diagram participant**

In the mermaid `sequenceDiagram`, the steps still describe the right behavior, but relabel the two internal steps:

```
    Up->>Disk: claim .log -> rename .uploading (TraceFileStore)
    Up->>Up: parse batches, group by service.name (TraceUploader)
    Up->>Up: topologically order + remap SpanIds (TraceReplayer)
    Up->>AM: replay + ForceFlush(10s) (TracerProviderExportSink)
```

- [ ] **Step 4: Verify no stale references remain**

Run: `git --no-pager grep -n "SendPendingStatic\|ParseTraceFile\|GetTraceDirectory" -- wiki/`

Expected: no output.

- [ ] **Step 5: Commit**

```bash
git add wiki/02-deep-dive/telemetry.md
git commit -m "{Telemetry} Update telemetry wiki for the uploader refactor"
```

---

### Task 9: Full-solution verification

**Files:** none.

**Interfaces:**
- Consumes: everything from Tasks 1–8.
- Produces: nothing.

- [ ] **Step 1: Build the whole solution**

Run: `dotnet build azclips.slnx`

Expected: build succeeds. If restore fails downloading the GitHub Copilot npm package from `registry.npmjs.org` (a TLS handshake failure from `Core.Agent`), use the `building-the-solution` skill — that failure is unrelated to this change.

- [ ] **Step 2: Run the telemetry test suite one final time**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj`

Expected: PASS. Total should be the original 15 replay tests plus roughly 30 new ones.

- [ ] **Step 3: Confirm `FileTelemetrySender` is actually small now**

Run: `git --no-pager diff --stat main -- src/Core/Core.Telemetry/FileTelemetrySender.cs`

Expected: the file is down from 438 lines to roughly 25.

- [ ] **Step 4: Smoke-test the uploader against a scratch directory**

The uploader reads `%LocalAppData%\.azclips\traces`, which holds real pending telemetry. Move it aside first so the smoke run cannot delete anything, and restore it afterwards:

```powershell
$traces = Join-Path $env:LOCALAPPDATA '.azclips\traces'
$backup = "$traces.smoke-backup"
if (Test-Path $traces) { Rename-Item $traces $backup }
try {
    dotnet run --project src/CLI/Core.Cli.csproj -- --upload-telemetry
    Write-Output "exit code: $LASTEXITCODE"
}
finally {
    if (Test-Path $traces) { Remove-Item $traces -Recurse -Force }
    if (Test-Path $backup) { Rename-Item $backup $traces }
}
```

Expected: exit code 0, no output. This verifies the `--upload-telemetry` branch reaches `TelemetryUpload.RunPending()` and returns cleanly against an absent directory; the uploader's behavior with actual files is covered by `TraceUploaderTests`.

---

## Follow-up filed separately

Not part of this plan: the trace directory grows without bound when the endpoint is unreachable, and benchmark or in-process test runs write trace files that are never uploaded or cleaned up, because the uploader only runs when the host executable is `az`/`az.exe`. See the "Deferred" section of the design spec.
