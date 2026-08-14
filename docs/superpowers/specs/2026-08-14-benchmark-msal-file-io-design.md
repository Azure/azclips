# Measuring MSAL token-cache file IO in the in-process benchmarks

Date: 2026-08-14

## Problem

The in-process CLI and PowerShell benchmarks replace `IAuthService` with `StubAuthService`, a
purely in-memory stub that returns a canned `AuthResult`. The production implementation,
`MsalAuthService`, reads the MSAL token cache from disk on every invocation. That disk cost is
part of what a real `az` command pays and is currently invisible to the benchmarks.

We want the existing in-process command benchmarks to include that real file IO, while
guaranteeing that no MSAL network time — instance discovery, OIDC discovery, token issuance, or
refresh — is ever folded into the measurement.

## Goals

- The existing CLI and PowerShell command benchmarks exercise the real `MsalAuthService`,
  including its token-cache disk reads.
- Zero network time can enter a measurement. A network attempt is impossible in the steady
  state and fails loudly if it ever occurs.
- Measurements are deterministic on a developer machine and in CI, neither of which may have a
  real MSAL cache on disk.
- Production behavior is unchanged.

## Non-goals

- Isolating file IO into its own reported number. The benchmarks continue to report whole-command
  times; those times now include auth disk cost.
- Covering the startup benchmarks. They spawn a real `az` process out-of-process, so a DI
  override cannot reach them.
- Exercising the MSAL refresh path or the post-refresh cache write.

## Current state

`MsalAuthService.AcquireTokenSilentlyAsync` performs the following on each call:

1. `BuildApp(tenantId)` constructs an `IPublicClientApplication`. The client id is hardcoded to
   `1950a258-227b-4e31-a9cf-717495945fc2`.
2. `RegisterTokenCacheAsync(app, plaintextUnsafe: true)` resolves the cache directory from
   `HomePaths.GetDefaultLocalAppDataRoot()`, builds `StorageCreationProperties` for
   `MSAL.cache.plaintext`, then `MsalCacheHelper.CreateAsync` and `RegisterCache`. This is where
   the disk read happens.
3. `app.GetAccountsAsync()` touches the cache again.
4. `MsalAccountSelector.SelectAccount(accounts, profile)` picks the account. It returns `null`
   unless `profile.UserPrincipalName` is set and matches an account username.
5. `app.AcquireTokenSilent(...).ExecuteAsync(...)` — the only step that can reach the network.

Constraints this imposes:

- The cache directory is hardcoded with no injectable seam.
- `StubHttpTerminateHandler` only terminates the product's own HTTP pipeline. MSAL uses its own
  `HttpClient`, so MSAL network calls would be real network calls.
- `StubProfileService` seeds a default profile with no `UserPrincipalName`, so
  `SelectAccount` would return `null` and `AcquireTokenSilentlyAsync` would throw
  `MsalUiRequiredException` on every iteration.

## Design

### 1. Product seam: `MsalClientOptions`

New file `src/Core/Core.Authentication/MsalClientOptions.cs`:

```csharp
public sealed record MsalClientOptions
{
    public string CacheDirectory { get; init; } = HomePaths.GetDefaultLocalAppDataRoot();
    public IMsalHttpClientFactory? HttpClientFactory { get; init; }
}
```

- `DIRegisterExtensions.RegisterAuthenticationServices` adds `services.TryAddSingleton(new MsalClientOptions())`.
- `MsalAuthService` takes `MsalClientOptions Options` as a primary-constructor dependency.
- `RegisterTokenCacheAsync` reads `Options.CacheDirectory` in place of the direct
  `HomePaths.GetDefaultLocalAppDataRoot()` call.
- `BuildApp` and the builder inside `LogoutAsync` apply `.WithHttpClientFactory(Options.HttpClientFactory)`
  when it is non-null.

The default instance reproduces today's behavior exactly: the same cache root, and a null factory
means MSAL uses its own HTTP stack. No user-facing configuration surface is added, which is why
this is a dedicated DI record rather than a field on `CoreOptions`.

Only the two variable inputs are parameterized. The app-construction and cache-wiring logic stays
in `MsalAuthService`, so the benchmark measures the real production code path rather than a
benchmark-owned copy of it.

### 2. Benchmark harness

New files under `tests/Benchmark/`:

**`Fixtures/MsalCache.seed.json`** — a static MSAL token-cache document containing one
`Account`, one `AccessToken`, one `RefreshToken`, one `IdToken`, and one `AppMetadata` entry, with
synthetic secrets of realistic length so the file size is representative. Copied to the output
directory via a `Content` / `CopyToOutputDirectory` item, mirroring how `Tests.Common` copies its
fixtures.

The access-token entry carries an `expires_on` in the year 3026 (roughly 33.4 billion unix
seconds, comfortably inside `DateTimeOffset`'s year-9999 ceiling) and omits `refresh_on`. The
far-future expiry means the entry never expires during a run, and the absent `refresh_on` means
MSAL has no proactive-refresh trigger. Together these remove any reason for MSAL to contact the
network, and remove the need to stamp timestamps at setup time. A stale `cached_at` is harmless
because MSAL uses it for telemetry and refresh calculations, not for validity.

The fixture lives in `tests/Benchmark/Fixtures/` rather than `tests/Common/Stubs/Fixtures/`
because the latter is ARM-response-specific and `Tests.Common` does not reference
`Microsoft.Identity.Client`.

**`Auth/MsalCacheFixture.cs`** — the constants that must agree with both the fixture and the stub
profile (`UserPrincipalName`, tenant `00000000-0000-0000-0000-000000000000`, MSAL's
`1950a258-227b-4e31-a9cf-717495945fc2` client id, the ARM default scope, and the known
access-token secret), plus `Seed(string cacheDirectory)` which copies the seed document to
`MSAL.cache.plaintext` in the target directory. Its XML doc records that the year-3026 expiry is
deliberate, since JSON cannot carry a comment and MSAL's handling of unknown fields is not
something to rely on.

**`Auth/ThrowingMsalHttpClientFactory.cs`** — an `IMsalHttpClientFactory` returning an `HttpClient`
over a handler that throws, naming the URI it was asked to reach.

Changes to `tests/Benchmark/BenchmarkBase.cs`:

- `InitializeCommon()` creates and seeds `artifacts/Benchmark/home/msal` once per run, alongside
  the existing `artifacts/Benchmark/home/extensions` staging directory.
- `RegisterBenchmarkStubs()` no longer registers `StubAuthService`. It registers:
  - `MsalClientOptions` with `CacheDirectory` pointing at the scratch directory and
    `HttpClientFactory` set to `ThrowingMsalHttpClientFactory`.
  - A `StubProfileService` instance seeded with `MsalCacheFixture.UserPrincipalName`.
  - `IAuthService` is deliberately left to the product registration, so the real `MsalAuthService`
    is resolved.

`AzclipsServiceProvider.OverrideRegistration` runs after the default registrations with last-wins
resolution, so the explicit `AddSingleton` of `MsalClientOptions` takes precedence over the
default `TryAddSingleton`.

Changes to `tests/Common/Stubs/StubProfileService.cs`: an optional `userPrincipalName` constructor
parameter defaulting to `null`, applied to the seeded default profile. Existing callers in
`Cli.Tests` and `Scenario.Tests` are unaffected because the default preserves current behavior.

`GlobalCleanup` already deletes `artifacts/Benchmark/home` on a best-effort basis, which now covers
the seeded cache directory.

### 3. Measurement boundary and guards

With `[InvocationCount(1)]`, BenchmarkDotNet excludes `[IterationSetup]` from the measured region,
so stub registration, runspace creation, and module import stay outside it. Auth runs inside
`Engine.CreateProcessorContext` → `AuthHandler.AcquireTokenAsyncDefault` →
`MsalAuthService.AcquireTokenSilentlyAsync`, which is inside `RunCli` / `RunPowerShellCommand` and
therefore inside the measured region.

`IterationCleanup` calls `CleanupCommon()`, which disposes the DI container and clears
registrations. The MSAL application and cache helper are rebuilt on the next iteration, so the
disk read repeats per iteration rather than being amortized to nothing.

Disk work captured per iteration: the directory probe and cross-process lock file in
`MsalCacheHelper.CreateAsync`, the initial cache load in `RegisterCache`, and the cache access in
`GetAccountsAsync`. `AcquireTokenSilent` then resolves from the in-memory cache.

**Guard 1 — network.** Any MSAL HTTP attempt throws. The exception fails the CLI or cmdlet run,
which fails the BenchmarkDotNet report, which `Program.InspectSummary` converts into a non-zero
exit code. This catches instance discovery and OIDC discovery, not only token issuance.

**Guard 2 — fixture drift.** `AuthResult` is deliberately MSAL-independent and does not expose
`AuthenticationResultMetadata.TokenSource`, so the check is structural rather than metadata-based.
`GlobalSetup` mirrors exactly what an iteration does — `RegisterBenchmarkStubs()`,
`AzclipsServiceProvider.Initialize(...)`, resolve `IAuthService`, one throwaway
`AcquireTokenSilentlyAsync`, then `AzclipsServiceProvider.Dispose()` — and asserts the returned
`AccessToken` and `UserPrincipalName` equal the fixture's known values. Because Guard 1 makes a
network-sourced token impossible, a matching token proves the value was read from disk. A wrong
client id, wrong scope, or UPN mismatch fails setup rather than silently changing which code path
is measured. Setup failure surfaces as `[Setup] ...: no reports produced` in the runner's verdict.
Expiry drift is not a concern given the static far-future expiry, but the assertion covers it at
no extra cost.

A failure in either guard is loud by design: a degraded run must never reach the dashboard, since
a silently different code path would move the numbers for the wrong reason.

### 4. Consequence for the performance gate

Reported numbers remain whole-command times and now include auth disk cost, so they will rise.
The introducing pull request will read as a regression against the historical baseline and the
115% gate will need a one-time rebaseline when it merges to `main`. This is accepted and expected.

## Testing

- `tests/Cli.Tests`, following the `MethodName_ShouldBehavior_WhenCondition` convention:
  - `MsalAuthService` honors `MsalClientOptions.CacheDirectory`, verified against a temp directory.
  - `MsalAuthService` applies the injected `IMsalHttpClientFactory`.
  - A request issued through `ThrowingMsalHttpClientFactory` throws with the URI in the message.
- Run `Cli.Tests` and `Scenario.Tests` to confirm the additive `StubProfileService` parameter
  changed nothing.
- Build with `dotnet build azclips.slnx`, using the `building-the-solution` skill for the known
  npm/TLS restore quirk.
- Smoke-run the benchmarks with the `validate-benchmarks` skill, which temporarily lowers warmup
  and iteration counts to 5, runs in local mode, and restores them to 5000. This proves the seeded
  cache produces a cache hit and the tripwire never fires, without a multi-hour run.

## Documentation

`wiki/02-deep-dive/testing-benchmarking.md` states that `BenchmarkBase.RegisterBenchmarkStubs()`
registers `StubAuthService`, and its stub diagram lists auth among the stubbed handlers. Both
become inaccurate and are updated to describe the real `MsalAuthService` running against a seeded
cache with a throwing MSAL HTTP factory.

## Alternatives considered

**Canned in-memory MSAL HTTP responses** instead of a throwing handler. This would let a refresh
complete without real network IO and would additionally exercise the post-refresh cache write. It
was rejected because it re-admits a code path where MSAL performs a refresh we did not intend, the
in-memory handler still contributes non-file-IO time to the measurement, and the canned bodies
must stay schema-correct or MSAL throws anyway. With a tripwire, no network path exists at all.

**Reading the developer's real `%LOCALAPPDATA%\.azclips` cache.** Requires no product change, but
is non-deterministic across machines and effectively empty in CI, where reading a missing file is
far cheaper than reading a real one and would understate the disk cost.

**A dedicated auth file-IO micro-benchmark** rather than folding the cost into the command
benchmarks. Rejected because the goal is for existing command numbers to reflect what a real
invocation pays.

**Extending `CoreOptions` via `IConfigurationService`** for the seam. Rejected because
`CoreOptions` is user-facing serializable configuration; an `IMsalHttpClientFactory` does not
belong there, and a cache-root knob is unwanted public surface.

**Replacing app construction with an `IMsalPublicClientFactory` abstraction.** Rejected because
the benchmark would then exercise a benchmark-owned copy of the wiring rather than the production
code path.
