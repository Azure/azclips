# TEMP-PR-PLAN — Issue #470

**Issue:** [\[Private Preview Internal Feedback\] - Powershell session and config issues](https://github.com/Azure/azclips/issues/470)
**Reporter:** @teresaritorto
**Milestone:** Cirrus - May 2026 (yonzhan flagged as "not a functional blocker — fix after private preview")
**Branch:** `clips-470-powershell-session-config-issues`

---

## 1. Reported symptom

Inside a long-lived PowerShell session:

1. Import an `Az.*` binary module while `telemetry.enabled = true`.
2. Run at least one cmdlet — a span lands in App Insights (expected).
3. Run `Set-AzConfig -Key telemetry.enabled -Value false`.
4. Run any subsequent cmdlet.
5. **Observed:** the cmdlet still emits a telemetry span to App Insights.
6. **Reporter's workaround:** `Remove-Module` first, then `Set-AzConfig`, then re-import — spans stop.

## 2. Root cause

`PSTelemetryProvider` is registered as a **singleton** ([src/PS/Login/ModuleInitializer.cs:21](src/PS/Login/ModuleInitializer.cs#L21)). Its constructor reads `TelemetryOptions.Enabled` **once** and either wires up the full OpenTelemetry pipeline or short-circuits ([src/PS/Login/Telemetry/PSTelemetryProvider.cs:20-21](src/PS/Login/Telemetry/PSTelemetryProvider.cs#L20-L21)). The static `AzclipsServiceProvider` container lives from `Import-Module` to `Remove-Module`, so that decision is frozen for the whole PS session — even though `IConfigurationService.GetConfiguration<T>()` itself re-reads `~/.azclips/config.json` on every call.

The `DefaultActivityNameFilterProcessor` uses a strict allowlist ([src/Core/Core.Telemetry/DefaultTelemetryActivityNameFilter.cs](src/Core/Core.Telemetry/DefaultTelemetryActivityNameFilter.cs)) that only lets through operation names we own (`PowerShell.Activity.Root`, `LoginProcessor.*`, `SetConfigCommand.*`, `ActionStart.*`, …). Every allowlisted name is created by our own code via `ITelemetryService.StartActivity` / `StartRootActivity`. The only two direct `ActivitySource.StartActivity` call sites in `src/**` are inside `TelemetryService` itself. So **nothing reaches Azure Monitor unless it flows through `ITelemetryService.Start*` first** — which means gating those methods on a live config read is a complete fix.

## 3. Affected hosts

| Host           | Container lifetime                     | Bug observable?                      |
| -------------- | -------------------------------------- | ------------------------------------ |
| **PowerShell** | Import-Module → Remove-Module (hours+) | **Yes** (this issue)                 |
| **TUI**        | REPL until user quits (long)           | **Yes** (latent; same fix)           |
| **CLI**        | One process = one `az` invocation      | No — new process = fresh config read |
| **Agent**      | Spawned per CLI-failure, medium-lived  | Latent; inherits CLI provider        |

The fix lives in `TelemetryService` + a two-line delete in each of the three provider ctors, so it covers PS/TUI/CLI in the same PR at zero incremental cost.

---

## 4. Implementation plan

### 4.1 Behavior after the fix

- `TelemetryService.StartActivity` and `StartRootActivity` re-read `TelemetryOptions.Enabled` on every call. When `false`, they return `null` and no downstream `Activity` is created for that call → no span reaches the exporter.
- The provider ctors always build the `TracerProvider` (regardless of `Enabled`), so the pipeline is present and ready. The service-level gate is the single source of truth — flipping `telemetry.enabled` in either direction takes effect on the next cmdlet without a module reload.
- Cost: one `configurationService.GetConfiguration<TelemetryOptions>()` call per activity start. Warm-cache estimate ~5–15 µs per call, or ~50–300 µs added to a cmdlet that already spends hundreds of ms on network I/O. Not user-observable. See §7 for follow-up options if a benchmark ever flags it.

### 4.2 File changes

**A. [src/Core/Core.Telemetry/TelemetryService.cs](src/Core/Core.Telemetry/TelemetryService.cs) — add the runtime gate**

- Add `IConfigurationService` as a ctor param (constructor injection picks it up automatically from DI; no registration change needed).
- Add a private `IsTelemetryEnabled()` helper that returns `_configurationService.GetConfiguration<TelemetryOptions>().Enabled`.
- Guard `StartActivity` and `StartRootActivity` with `if (!IsTelemetryEnabled()) return null;` as the first line.
- Leave `TagInvocation` / `AddTag` untouched — they operate on `Activity.Current`, which is `null` when the root was gated off, so tags become no-ops naturally.
- Leave `Dispose` untouched.

**B. [src/PS/Login/Telemetry/PSTelemetryProvider.cs](src/PS/Login/Telemetry/PSTelemetryProvider.cs) — drop ctor early-exit**

- Delete the two lines:

    ```csharp
    var isTelemetryEnabled = configurationService.GetConfiguration<TelemetryOptions>().Enabled;
    if (!isTelemetryEnabled) return;
    ```

- If `configurationService` becomes unused after removal, drop it from the ctor signature as well.

**C. [src/CLI/Telemetry/CLITelemetryProvider.cs](src/CLI/Telemetry/CLITelemetryProvider.cs) — same two-line delete**

Same rationale (parity). Not strictly required for the bug fix — the CLI process is single-shot — but keeps the three providers symmetrical and closes the latent pattern for any future long-lived CLI reuse.

**D. [TUI/Telemetry/TUITelemetryProvider.cs](TUI/Telemetry/TUITelemetryProvider.cs) — same two-line delete**

Same rationale, but this one is user-observable in the TUI REPL.

**E. Docs — [wiki/02-deep-dive/telemetry.md:51](wiki/02-deep-dive/telemetry.md#L51)**

Current text:

> Each provider reads `TelemetryOptions.Enabled` and returns immediately when telemetry is disabled…

Update to describe the new behavior: the check now lives in `TelemetryService` and is evaluated on every `Start*` call, so toggling `telemetry.enabled` is live in both directions.

### 4.3 What this fix does not change

- `IConfigurationService` interface — no new events, no new abstractions.
- DI registrations — no changes required; MS.DI resolves the new ctor param automatically.
- Any existing behavior for users who never change config mid-session.
- Non-telemetry stale-config surfaces (e.g. `Engine._extensionIndices` cache size) — separate follow-up.

---

## 5. Reproduce guides

Both guides assume a local build from this branch. Prep once:

```powershell
# from repo root
dotnet build azclips.slnx -c Debug
```

Local build output: `artifacts/Debug/` (module manifests, DLLs). For the CLI, use `dotnet run --project src/CLI/Core.Cli.csproj -- <args>` unless a packaged `bin/az.exe` is already present from a prior packaging step.

Two ways to observe whether telemetry actually leaves the process (pick whichever is easier for you locally):

- **Watch outbound traffic** to `dc.services.visualstudio.com` (App Insights ingestion) with Fiddler / mitmproxy. Cleanest, no code changes.
- **Temporary probe:** drop a `Console.Error.WriteLine($"[TELEMETRY-PROBE] {name}");` at the top of `TelemetryService.StartRootActivity` and `StartActivity` before running the repro; delete before commit.

The instructions below use the probe approach so they're self-contained.

### 5.1 Reproduce in PowerShell (should currently repro; should not after fix)

```powershell
# from repo root, in a fresh pwsh session with the temporary probe in place

# 1. Ensure telemetry starts enabled (delete any existing user config)
Remove-Item $HOME/.azclips/config.json -ErrorAction SilentlyContinue

# 2. Import the freshly-built modules
Import-Module ./artifacts/Debug/Login.psd1
Import-Module ./artifacts/Debug/Config.psd1

# 3. Run one cmdlet to force PSTelemetryProvider ctor to run
Get-AzConfig -Key telemetry.enabled
# Expect: [TELEMETRY-PROBE] lines printed.

# 4. Disable telemetry
Set-AzConfig -Key telemetry.enabled -Value false

# 5. Confirm the config file now says false
Get-Content $HOME/.azclips/config.json

# 6. Run another cmdlet
Get-AzConfig -Key telemetry.enabled
# BUG (current main):  [TELEMETRY-PROBE] lines are STILL printed → spans still emitted.
# FIXED behavior:      no [TELEMETRY-PROBE] lines printed → gate short-circuited.
```

Baseline sanity check (proves the reporter's workaround, which already works on main):

```powershell
# From the same shell, continuing from step 6.
Remove-Module -Name Login, Config, Compute, Resources -Force -ErrorAction SilentlyContinue
Import-Module ./artifacts/Debug/Login.psd1
Import-Module ./artifacts/Debug/Config.psd1
Get-AzConfig -Key telemetry.enabled
# Expect on main: no probe lines. The Remove-Module workaround is what Teresa found.
```

### 5.2 Prove the bug is NOT observable in the CLI

```powershell
# Clean starting state
Remove-Item $HOME/.azclips/config.json -ErrorAction SilentlyContinue

# 1. Run any command while enabled — probe should fire
dotnet run --project src/CLI/Core.Cli.csproj -- group list
# Expect: [TELEMETRY-PROBE] lines printed.

# 2. Disable telemetry
dotnet run --project src/CLI/Core.Cli.csproj -- config set --key telemetry.enabled --value false

# 3. Run another command
dotnet run --project src/CLI/Core.Cli.csproj -- group list
# Expect on today's main: NO probe lines from step 3's process.
#   → because step 3 is a brand-new process that reads fresh config at startup and
#     PSTelemetryProvider's equivalent early-exit in CLITelemetryProvider skips the pipeline.
# This is the "not observable in CLI" claim; the guide exists to prove it.
```

If step 3 does emit a span on main, then the CLI is also affected today and the CLI-side change becomes load-bearing. Current expectation: it won't, because CLI processes don't outlive a config change.

### 5.3 Manual repro artifact

Drop `scratch/repro-470.ps1` with the PS steps above scripted end-to-end (echoes each step, prints `Get-Content config.json` between them). Keep it uncommitted / gitignored — dev-loop convenience only.

---

## 6. Tests required

### 6.1 Unit test — `tests/Core.Telemetry.Tests/TelemetryServiceEnabledGateTests.cs` (new)

Purpose: lock in that `TelemetryService` reads config on every call and honors it.

```
Arrange:
  - Stub IConfigurationService whose GetConfiguration<TelemetryOptions>() reads a mutable field.
  - Real DefaultTelemetryProvider (or a simple ITelemetryProvider stub) with an
    ActivitySource that a test ActivityListener subscribes to.
  - Construct TelemetryService(provider, stubConfig, ClientType.PowerShell).

Cases:
  1. Enabled=true → StartRootActivity() returns non-null AND listener records it.
  2. Flip Enabled=false → StartRootActivity() returns null AND listener does NOT record.
  3. Flip Enabled=true → StartRootActivity() returns non-null again (symmetry proof).
  4. Same three cases for StartActivity(name).
  5. Enabled=false at Start time → TagInvocation is a no-op (no Activity.Current to tag).
```

Should compile-fail on main (ctor signature changes) or assertion-fail (case 2). Passes after the fix.

### 6.2 Unit test — `tests/Core.Telemetry.Tests/TelemetryProviderCtorTests.cs` (new, small)

Purpose: guard against regressing the ctor early-exit removal — construct `PSTelemetryProvider` (and CLI/TUI counterparts) with a stub `IConfigurationService` returning `Enabled=false` and assert that the resulting `ActivitySource` name equals `"PowerShell"` (etc.), not `"Default"`. Prevents a future refactor from silently re-introducing the ctor gate.

### 6.3 Integration test — `tests/Cmdlets.Tests/TelemetryToggleCmdletsTests.cs` (new)

Purpose: end-to-end proof that mimics the reporter's flow.

```
Arrange:
  - Isolated TEMP config path (redirect via env override or HomePaths override in the fixture).
  - Register a capturing test BaseProcessor<Activity> via PrependRegistration that records
    exported activity names/tags.
  - Import Login + Config module assemblies into a hosted PowerShell runspace using the
    existing Cmdlets.Tests plumbing.

Act:
  1. Run Get-AzConfig cmdlet → 1 root activity captured.
  2. Run Set-AzConfig telemetry.enabled false.
  3. Run Get-AzConfig again.

Assert:
  - After step 3, no NEW activities recorded beyond step 2's own Set span
    (i.e. step 3's Get-AzConfig root activity should not appear).
```

### 6.4 Existing suites to re-run

- `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj`
- `dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj`
- `dotnet test tests/Cli.Tests/Cli.Tests.csproj` (regression net for the CLI provider tweak)

---

## 7. Follow-ups outside this PR

- File a companion issue for `Engine._extensionIndices` cache size staleness (low priority — cache size only, not correctness).
- Consider promoting `TelemetryOptions` reads to `IOptionsMonitor<TelemetryOptions>` (or a short-TTL memoization inside `ConfigurationService`) if per-call read cost ever shows up in a benchmark.
- Wiki: mention that telemetry opt-out is now immediate in both directions ([wiki/onboarding-guide.md:621](wiki/onboarding-guide.md#L621), [wiki/02-deep-dive/telemetry.md](wiki/02-deep-dive/telemetry.md)).
