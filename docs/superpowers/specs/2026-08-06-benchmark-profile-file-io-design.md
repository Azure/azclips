# Real profile file I/O in benchmarks

Date: 2026-08-06
Status: Approved

## Problem

`BenchmarkBase.RegisterBenchmarkStubs()` registers `StubProfileService` as the
`IProfileService` implementation for every benchmark. That stub keeps profiles in a
`Dictionary` and returns completed tasks, so no file is ever touched.

The production implementation, `Core.Runtime.ProfileService`, persists profiles as JSON in
`{LocalApplicationData}/.azclips/profiles.json` and tracks the active profile in a separate
`active-profile` file. Every write is atomic: serialize, write a temp file, then rename over
the destination.

Benchmarks therefore under-report the cost of any command that persists a profile. `az login`
is the clearest case: `LoginProcessor.ProcessAsync` ends with
`SaveAndActivateProfileAsync`, which in production reads `profiles.json`, rewrites it, and
rewrites the active-profile pointer — three file operations the benchmark currently measures
as three dictionary assignments.

## Goals

- Benchmarks measure the real profile file I/O: JSON read and deserialize, serialize, atomic
  temp-write plus rename, for both `profiles.json` and `active-profile`.
- The measured code is the production code. No reimplementation of persistence logic in test
  code, so the benchmark cannot silently drift away from what ships.
- Benchmark runs never read or write the developer's real `~/.azclips` directory.
- Unit and scenario tests keep their fast in-memory profile stub.

## Non-goals

- Changing `ProfileService` itself.
- Measuring the first-ever invocation (cold, no `profiles.json` on disk). See "Warm vs cold"
  below.
- Changing the other benchmark stubs: `StubHttpTerminateHandler`, `StubTelemetryService`,
  and `StubAuthService` are unaffected.

## Approach

Register the real `ProfileService` in the benchmark DI override, pointed at a
benchmark-scoped directory.

`ProfileService` already accepts an optional `baseDirectory` constructor argument, and
`Benchmark.csproj` already references `Core.Runtime`. No new project references and no new
production code are needed.

### Alternatives considered

**Add file I/O inside `StubProfileService`.** Rejected. It would duplicate the serialization
and atomic-write logic in a test stub, so the benchmark would measure the stub's I/O rather
than the product's. If `ProfileService` later changed its file format or added locking, the
benchmarks would keep reporting the old cost with no failing test to catch it. That defeats
the goal.

**Make `StubProfileService` delegate to a real `ProfileService` when given a directory.**
Rejected. It would force `Tests.Common` to take a dependency on `Core.Runtime`, which today
it does not have — it references only `Core.Abstractions` and `Core.Extensions`. That layering
change affects every test project, in exchange for keeping the word "Stub" in the benchmark's
DI registration.

### Scope: base class, not per-benchmark

The registration change lives in `BenchmarkBase`, so it applies to all CLI and PS benchmarks
rather than only the login ones.

Today `IProfileService` is consumed solely by the Login extension processors, so this only
changes login benchmark numbers in practice. Putting it in the base means any processor that
later takes a dependency on `IProfileService` is measured correctly by default, instead of
silently reverting to in-memory timings until someone notices.

### Warm vs cold

Each measured iteration starts from a seeded, already-existing profile store.

A cold store, where `profiles.json` does not exist, is the first-ever invocation on a machine.
`LoadProfilesAsync` short-circuits on `File.Exists` and returns an empty dictionary, so no read
is measured at all — only a directory create, temp write, and rename. That is not the path
users take after installation.

Seeding once at setup means every iteration measures the steady state: read, deserialize,
serialize, and two atomic writes.

## Design

### `BenchmarkProfileStore` (new)

A small class in the `Benchmark` project owning the benchmark's profile directory.

- `Directory` resolves to `<repoRoot>/artifacts/Benchmark/home/.azclips-{Environment.ProcessId}`.
  It sits under the existing `artifacts/Benchmark/home` tree that `CLIBenchmarkBase` already
  creates and cleans. The process-id suffix guarantees each BenchmarkDotNet child process gets
  clean seeded state and that two concurrent benchmark invocations can never collide on the
  same files.
- `Initialize(repoRoot)` deletes any existing directory, recreates it, then seeds it by calling
  `SaveAndActivateProfileAsync` on a real `ProfileService` instance with the same `default`
  profile `StubProfileService` seeds today: tenant and subscription all-zero GUIDs, cloud
  `AzureCloud`, auth method `Browser`. Seeding through `ProfileService` rather than
  hand-written JSON guarantees the on-disk format matches production exactly and keeps the
  serialization options in one place.
- `Delete()` removes the directory, best-effort.
- A private nested no-op `ILoggingService` is used for seeding. `ProfileService` logs a
  `Verbose` line on each load and save, and with no active logging scope
  `ScopedLoggingService` falls back to `ConsoleLoggingService`, which writes to stderr.
  `Tests.Common` has no shared stub logger, so the store supplies its own.

### `BenchmarkBase`

- Exposes a `protected BenchmarkProfileStore ProfileStore` instance so derived benchmark
  bases can reach it for cleanup.
- `InitializeCommon()`, already called by both `CLIBenchmarkBase.GlobalSetup` and
  `PSBenchmarkBase.GlobalSetup`, initializes the store after resolving `RepoRoot`.
- `RegisterBenchmarkStubs()` replaces

  ```csharp
  services.AddSingleton<IProfileService, StubProfileService>();
  ```

  with

  ```csharp
  services.AddSingleton<IProfileService>(sp =>
      new ProfileService(sp.GetRequiredService<ILoggingService>(), ProfileStore.Directory));
  ```

  The other three stub registrations are unchanged.

### `PSBenchmarkBase`

Gains a `[GlobalCleanup]` that calls `ProfileStore.Delete()`. It currently has none, so there
is no conflict — BenchmarkDotNet permits only one `[GlobalCleanup]` per type hierarchy, and
`CLIBenchmarkBase` already declares one. The CLI side needs no change because its existing
cleanup deletes the whole `artifacts/Benchmark/home` tree, which contains the new directory.

For the same reason, the one-time store initialization hangs off `InitializeCommon()` rather
than a new `[GlobalSetup]`: both bases already declare one.

### `StubProfileService`

Behaviour unchanged. It remains the in-memory `IProfileService` for `Cli.Tests`,
`Scenario.Tests`, and `ConfigCliTests`. Its XML doc gains a note that benchmarks deliberately
use the real `ProfileService` instead.

### Documentation

`wiki/02-deep-dive/testing-benchmarking.md` states that `RegisterBenchmarkStubs()` registers
`StubProfileService`. That line is updated to describe the real file-backed service and the
benchmark-scoped directory.

## What gets measured

Per login iteration, inside the measured region:

1. Read and deserialize `profiles.json`.
2. Serialize the profile dictionary.
3. `Directory.CreateDirectory`, write `profiles.json.{guid}.tmp`, rename over `profiles.json`.
4. The same atomic write cycle for `active-profile`.

The store holds two entries — the seeded `default` plus the `temp` profile that bare login
writes — and stays at two for every subsequent iteration, so file size does not grow across a
run.

## Risks

**Transient `IOException` on Windows.** At 5000 warmup plus 5000 measured iterations per case,
a run performs roughly 20,000 atomic write cycles. `File.Move(overwrite: true)` maps to
`MoveFileEx`, which can fail transiently if Defender or the search indexer momentarily holds
the destination handle. Placing the store under `artifacts/` reduces exposure, and the
process-scoped directory removes cross-run contention. This is genuine production behaviour
rather than harness-only risk, so it is accepted and observed rather than worked around.

**Longer benchmark runs.** Roughly 20,000 small file writes per case add measurable wall-clock
time to a full run. This is the intended cost of measuring what was previously skipped.

**Stderr noise.** `ProfileService` logs `Verbose` on every load and save. On the CLI path this
is gated by `CliLoggingPolicy.ShouldEmit`, so at the default log level it is a delegate call
and a comparison with no console write. Seeding uses a no-op logger. Validation should confirm
no `VERBOSE: Loading profiles from` flood appears in benchmark output.

## Out of scope by construction

`StartupBenchmarkBase` does not derive from `BenchmarkBase` — it declares its own `RepoRoot`
and `InitializeCommon` — so the startup benchmark family is unaffected.

## Validation

1. `dotnet build azclips.slnx` succeeds.
2. `Cli.Tests` and `Scenario.Tests` pass, confirming `StubProfileService` consumers are
   unaffected.
3. The `validate-benchmarks` skill runs the suite in local mode with measurement iterations
   lowered to 5, confirming login benchmarks complete end to end and produce reports.
4. Inspect the run output: benchmark reports succeed, no verbose flood on stderr, and
   `artifacts/Benchmark/home` contains no leftover `.azclips-*` directory after a CLI run.

A `[GlobalSetup]` failure surfaces correctly today: BenchmarkDotNet swallows the exception but
produces no reports, and `Program.InspectSummary` already converts that into a non-zero exit
code.
