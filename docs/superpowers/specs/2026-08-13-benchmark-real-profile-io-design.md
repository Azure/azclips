# Real profile file IO in benchmarks

## Problem

`BenchmarkBase.RegisterBenchmarkStubs()` registers `StubProfileService`, an in-memory
`Dictionary<string, Profile>`. Every benchmark therefore resolves its profile from memory, while a
real user resolves it from disk. The published numbers understate the cost of every `az` and
PowerShell invocation by the amount of profile file IO the user actually pays.

Per invocation, `Engine.ResolveProfileAsync` calls `GetActiveProfileNameAsync()` and then
`GetProfileAsync(name)`. Against the real `ProfileService` that is two file existence checks, two
`ReadAllTextAsync` calls, and one JSON deserialize — none of which the benchmarks currently measure.

## Goal

Benchmarks exercise the real, file-backed `ProfileService` against an isolated, seeded profile store,
so measured timings include genuine profile file IO.

Non-goals: no production code changes, no new benchmark methods, no change to unit-test stubbing.

## Constraints

**Environment-variable redirection does not work in-process on Windows.** `ProfileService` resolves
its default directory through `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)`, which
on Windows reads the Win32 shell API rather than `%LOCALAPPDATA%`. `tests/Cli.Tests/ManagedIdentityE2ETests.cs`
documents this and only gets away with env-var redirection because it drives the CLI as a
subprocess. In-process benchmarks must therefore inject the directory directly.

**The store must be seeded, or the change is a no-op.** `ResolveProfileAsync` short-circuits and
returns null when no active profile name is found, before `GetProfileAsync` is ever called. An empty
store would cost a single `File.Exists` and measure almost nothing.

**Seeding runs outside the DI container.** `RegisterBenchmarkStubs()` only enqueues registration
lambdas; `AzclipsServiceProvider.Initialize` does not run until the measured `RunCli` call. Anything
the seeder needs must be hand-constructed.

## Approach

Use the `baseDirectory` seam that `ProfileService` already exposes:

```csharp
public ProfileService(ILoggingService logger, string? baseDirectory = null)
```

DI currently leaves `baseDirectory` null, so production uses `{LocalApplicationData}/.azclips`.
Benchmarks pass an explicit isolated directory instead. This requires no product code change and
mirrors how `CLIBenchmarkBase` already redirects the extensions home.

Two alternatives were considered and rejected. Adding a home-root seam to product code
(`IHomePathProvider` or a config key) is cleaner and would let the startup benchmarks reuse it, but
it changes shipping code and DI wiring for a benchmark-only need. An environment variable read by
`ProfileService` would work both in-process and out-of-process, but adds an undocumented global knob
to product code.

## Components

### `BenchmarkProfileStore` (new — `tests/Benchmark/`)

Owns the store location and seeding.

- `Root` — `{RepoRoot}/artifacts/Benchmark/home/.azclips`, a sibling of the existing
  `{RepoRoot}/artifacts/Benchmark/home/extensions`. Nesting `.azclips` inside `home` keeps the
  profile store visually distinct from the extensions home and prevents `CLIBenchmarkBase`'s
  `TryDeleteDirectory(home/extensions)` from interacting with profile files.
- `Seed()` — deletes any existing `profiles.json` and `active-profile`, then writes them by calling
  `SaveAndActivateProfileAsync` on a real `ProfileService` pointed at `Root`. Going through the
  production write path guarantees the on-disk bytes match exactly what production produces, with no
  duplicated `JsonSerializerOptions`.
- `VerifySeeded()` — reads the store back and throws unless the active profile resolves to a
  non-null profile named `default`.

The seeded profile matches what `StubProfileService` provided, so downstream auth and telemetry
behavior is unchanged:

| Field | Value |
| --- | --- |
| `Name` | `default` |
| `TenantId` | `00000000-0000-0000-0000-000000000000` |
| `SubscriptionId` | `00000000-0000-0000-0000-000000000000` |
| `CloudName` | `AzureCloud` |
| `AuthMethod` | `Browser` |

### `NullLoggingService` (promoted — `tests/Common/Stubs/`)

`ProfileService` requires an `ILoggingService`, and the seeder runs outside DI so it must supply one.

The real `ScopedLoggingService` is unsuitable here. It falls back to `ConsoleLoggingService`, whose
`Verbose` call reaches `ConsoleSink.Verbose`, which writes to stderr unconditionally — there is no
verbosity gate. `ProfileService` logs once per file written, so with roughly 10,000 seeds per
benchmark method (see Risks) seeding would emit on the order of 20,000 colored `VERBOSE:` lines,
interleaved with BenchmarkDotNet's own output.

This does not introduce a new pattern. The repository already contains four equivalent types:
private `NullLoggingService` classes in `tests/Cli.Tests/AssemblyLoading/ExtensionSelectionPathOverrideTests.cs`,
`CLIAssemblyLoadingProviderTests.cs`, and `BundledExtensionLoaderTests.cs`, plus a private
`NoOpLoggingService` in `src/CLI/CliExtensionServiceRegistration.cs`. This change adds one shared
copy under `tests/Common/Stubs/` alongside the existing stubs. Collapsing the three private
`Cli.Tests` copies into it is deliberately out of scope.

Note that this stub is used **only by the seeder**. Inside the measured region the DI-registered
`ProfileService` receives the real `ILoggingService` via `sp.GetRequiredService<ILoggingService>()`,
exactly as in production.

### `BenchmarkBase` (modified)

- `InitializeCommon()` additionally creates `BenchmarkProfileStore.Root`, seeds it, and calls
  `VerifySeeded()`.
- `RegisterBenchmarkStubs()` replaces `services.AddSingleton<IProfileService, StubProfileService>()`
  with a factory registering `new ProfileService(sp.GetRequiredService<ILoggingService>(), root)`.
  The other three stubs (HTTP, telemetry, auth) are unchanged.
- New `CleanupGlobal()` removes the store, best-effort, matching the existing `TryDeleteDirectory`
  convention. `CLIBenchmarkBase`'s existing `[GlobalCleanup]` already deletes the whole `home`
  directory and so covers the store implicitly; `CleanupGlobal()` exists so the PS base, which has
  no cleanup today, gets the same guarantee without duplicating the logic.

### `PSBenchmarkBase` (modified)

Gains a `[GlobalCleanup]` calling `CleanupGlobal()`. It has none today.

## Lifecycle

| Phase | Action | Measured |
| --- | --- | --- |
| `[GlobalSetup]` | Create store root, seed, verify | No |
| `[IterationSetup]` | `Seed()`, then `RegisterBenchmarkStubs()` | No |
| Benchmark method | `RunCli` / cmdlet → `Engine.ResolveProfileAsync` → two real file reads + JSON deserialize | **Yes** |
| `[IterationCleanup]` | Dispose DI container | No |
| `[GlobalCleanup]` | Delete store | No |

The container is disposed every iteration, so a fresh `ProfileService` is constructed each time and
no in-process caching hides the IO.

Re-seeding is unconditional in `IterationSetup`, so every iteration starts from identical bytes even
if a benchmark writes. Both read and write paths run against the real service; no method is carved
out as read-only.

## Error handling

Seeding failures and a failed `VerifySeeded()` throw with an explanatory message. Failing loudly is
required: a silently empty store still exits 0 while benchmarking a degraded, profile-less path.

Store deletion in `GlobalCleanup` is best-effort and swallows `IOException` and
`UnauthorizedAccessException`, consistent with `CLIBenchmarkBase.TryDeleteDirectory`.

## Validation

1. `dotnet build azclips.slnx`.
2. Existing unit tests are unaffected — `StubProfileService` remains in use by `Cli.Tests` and
   `Scenario.Tests` and is not removed.
3. Benchmark smoke run via the `validate-benchmarks` skill, confirming the suite still executes
   end-to-end.
4. Before/after `dotnet run -c Release --project tests/Benchmark/Benchmark.csproj -- --local` on
   `main` versus this branch, to capture the per-benchmark delta.

## Risks

**One-time baseline shift.** Real file IO makes every measured invocation slower, and the PR gate
fails anything above 115% of the historical baseline. The delta is measured locally and documented in
the PR; the baseline re-establishes itself when the change merges to `main` and new numbers are
published.

**Benchmark stability.** With `InvocationCount(1)`, `IterationSetup` runs once per iteration, so
unconditional re-seeding runs at least 10,000 times per benchmark method (`WarmupCount(5000)` +
`IterationCount(5000)`, plus BenchmarkDotNet's pilot and overhead phases). `WriteFileAtomicAsync`
does a temp-file write plus rename for each of the two files. That is substantial file churn under
`artifacts/`, which is
susceptible to anti-virus scanning variance. This repository already carries an "Improve PS benchmark
stability" change, so the concern is live. Accepted deliberately in favour of simplicity; if variance
worsens, the fallback is to re-seed only when the store differs from the expected bytes, which makes
read-only benchmarks seed once and then no-op.

**Suite wall-clock time** grows by the seeding cost multiplied by the iteration count, though
seeding is excluded from the measured region.

## Out of scope

- Redirecting the startup benchmarks. They spawn real `az.exe` out-of-process and today read the
  developer's real `~/.azclips`, so their numbers already vary by machine. Fixing that means setting
  `LOCALAPPDATA` / `XDG_DATA_HOME` on the child process and carries its own baseline impact.
- New profile-specific benchmark methods covering save/activate.
- A product-code home-root seam.
- Consolidating the three private `NullLoggingService` copies in `Cli.Tests`.
- `ProfileService`'s own `// todo: testability` comment and its inlined `LocalApplicationData`
  literal, which duplicates `HomePaths.GetDefaultLocalAppDataRoot`.
