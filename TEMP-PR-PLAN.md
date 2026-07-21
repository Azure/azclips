# TEMP-PR-PLAN — Issue #493

**Issue:** [Azure/CLIPS#493 — Logging: Examine default log-level, call site level usage, and Writeline vs Error.Writeline](https://github.com/Azure/CLIPS/issues/493)
**Draft PR:** [Azure/azclips#575 — {Core} Update default log-level to `WARN`, adjust call sites, and add debug logging for extension loading](https://github.com/Azure/azclips/pull/575)
**Branch:** `clips-493-default-log-level-call-sites`
**Milestone:** AzCLIPS: July 5 – July 18
**Squad:** act-quality-productivity-squad

> This is a **planning / discussion doc only** — no code changes yet. It captures what the issue asks, what we found in the code today, and the options we want to socialize before implementing.

---

## 1. What the issue is asking

The issue raises **two related concerns** from PR [#313 (Fix/cli log scope propagation)](https://github.com/Azure/azclips/pull/313), which set the default `core.logging.level` to `error`:

1. **Default log level — is `error` right?**
    - PR #313 defaulted to `error` to reduce console noise out of the box.
    - However, `INFO` is arguably the shell/CLI convention (per [PowerShell about_logging_non-windows](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_logging_non-windows?view=powershell-7.6)).
    - If we move to `INFO` as default, we must audit **call-site severity** — many statements currently written at `Info` may actually be `Verbose`/`Debug`-appropriate.

2. **Stream selection — `Console.WriteLine` vs `Console.Error.WriteLine`**
    - Reporter points at [`src/CLI/Program.cs` L205–L237](https://github.com/Azure/azclips/blob/main/src/CLI/Program.cs#L205-L237), where **all non-output streams** (verbose, debug, warning, info) currently go to **stdout**.
    - CLI convention (outside of pwsh) is: **stdout = command result only**; everything else (verbose/debug/info/warn/error) goes to **stderr**. This matters for pipelines, JSON consumers, and tooling that redirects streams.

We're being asked to **investigate** both, not necessarily to change them yet.

### 1.b Additional scope items added to this PR

On top of the two issue asks, we're folding in two adjacent items so we don't lose them:

3. **Change the default log level from `error` to `warn`.**
    - Decision recorded 2026-07-21. Rationale in §3 Q0.

4. **Add debug statements for CLI extension loading.**
    - Today [`BuiltinExtensionLoader`](src/CLI/AssemblyLoading/ExtensionLoader.cs#L27-L150) and [`MultiVersionExtensionLoader`](src/CLI/AssemblyLoading/ExtensionLoader.cs#L280-L410) only log on the **negative path** ("not found", "skipping") and on individual `Load(AssemblyName)` calls. There is no trace of _which_ loader is being asked, _which candidate roots/directories/assembly names_ were tried before the miss, or of the _successful_ load. Debugging "why did my extension not resolve?" currently requires an attached debugger or hand-added `Console.WriteLine`s. Add well-labeled `Debug`/`Verbose` statements at the enter/probe/hit/miss points so `az --debug <cmd>` explains the resolution end-to-end.

---

## 2. What the code looks like today

### 2.1 Default log level

- Declared in [src/Core/Core.Abstractions/CoreOptions.cs](src/Core/Core.Abstractions/CoreOptions.cs#L15-L18):

    ```csharp
    public sealed class LoggingOptions
    {
        public string Level { get; set; } = "error";
    }
    ```

- Schema mirror in [src/Core/Core.Configuration/Schema/ConfigurationSchema.cs](src/Core/Core.Configuration/Schema/ConfigurationSchema.cs#L12):
    ```csharp
    new() { Key = "core.logging.level", DefaultValue = "error", AllowedValues = ["debug", "info", "warn", "error"] }
    ```
- Env override: `AZCLIPS_LOG_LEVEL` → `core.logging.level` (see [EnvironmentVariableMappings.cs](src/Core/Core.Configuration/Abstractions/EnvironmentVariableMappings.cs#L15-L19)).
- Parsed by two symmetric enums:
    - [CliLoggingPolicy.CliLogLevel](src/CLI/CliLoggingPolicy.cs#L8-L14) (CLI)
    - [CmdletOutputStreamsFactory.LogLevel](src/PS/Common/CmdletOutputStreamsFactory.cs#L11-L17) (PS cmdlets)
- Fallback when config is missing/invalid → `Error` (both places, [L42](src/CLI/CliLoggingPolicy.cs#L42) and [L122](src/PS/Common/CmdletOutputStreamsFactory.cs#L122)).
- `--debug` is a one-shot override that promotes level to `Debug` for that invocation ([Program.cs L92-L93](src/CLI/Program.cs#L92-L93)).

### 2.2 Stream selection today (CLI)

In [src/CLI/Program.cs `BuildStreams`](src/CLI/Program.cs#L203-L253):

| Stream             | Written to   | Prefix      |
| ------------------ | ------------ | ----------- |
| `WriteOutput`      | **stdout**   | (raw value) |
| `WriteError`       | **stderr** ✔ | `[ERROR] `  |
| `WriteVerbose`     | **stdout** ⚠ | `VERBOSE: ` |
| `WriteDebug`       | **stdout** ⚠ | `DEBUG: `   |
| `WriteWarning`     | **stdout** ⚠ | `WARNING: ` |
| `WriteInformation` | **stdout** ⚠ | `INFO: `    |

Only `WriteError` currently uses `Console.Error.WriteLine`. Everything else — even `WriteWarning` — goes to stdout, which will pollute pipelines and `--output json` consumers.

Other stdout-writers worth flagging in the audit:

- [ConsoleLoggingService](src/Core/Core.Runtime/ConsoleLoggingService.cs#L14-L56) — used as the **fallback** logger when no session scope is active. Every level (`Error`, `Warn`, `Verbose`, `Debug`) writes to **stdout** via `Console.WriteLine`. This is the "background/startup" path.
- [ConsoleOutputAdapter](src/Core/Core.Runtime/ConsoleOutputAdapter.cs#L17-L51) — `WriteErrorAsync` correctly uses stderr; `WriteWarningAsync` uses stdout.
- [MsalAuthService](src/Core/Core.Authentication/MsalAuthService.cs#L85-L86) — device-code login message goes to stdout via `Console.WriteLine`. This is arguably **correct** (it's a user-facing prompt), but worth confirming.

### 2.3 Call-site severity today (sample)

Quick audit via grep on `\.(Info|Verbose|Debug|Warn)\(`:

- **`Info` used for internal diagnostics** — will become user-visible if default flips to `info`:
    - [CLIArgumentParsingProvider.cs L23](src/CLI/ArgumentParsing/CLIArgumentParsingProvider.cs#L23) — `"Resolving alias '{key}' to canonical parameter '{canonicalKey}'"` → arguably `Verbose`.
    - [CLIAssemblyLoadingProvider.cs L21](src/CLI/AssemblyLoading/CLIAssemblyLoadingProvider.cs#L21) — `"Processing command with extension '{extensionName}'."` → arguably `Verbose`.
    - [Engine.Subscriptions.cs L124-L149](src/Core/Core.Engine/Engine.Subscriptions.cs#L124-L149) — several `Info` calls for subscription lifecycle events.
    - [TimingActivityListener.cs L52](src/Core/Core.Telemetry/TimingActivityListener.cs#L52) — `"[TIMING] ..."` timing traces at `Info`. Definitely `Debug`.
    - [ExtensionInstaller.cs L64-L98](src/Extensions/Extension/ExtensionInstaller.cs#L64-L98) — `Installing`, `Installed`, `Extracting` — these **are** the user-facing progress lines. Keep at `Info`.

- **`Verbose` / `Debug` — appear well-scoped** (auth, subscription pagination, extension loader diagnostics).

- **`Warn`** — sparingly used ([LoginProcessor.cs L419, L428](src/Extensions/Login/Processors/LoginProcessor.cs#L419-L428)); looks correct.

Conclusion: if we default to `Info`, we need a call-site pass to demote engine/plumbing chatter from `Info` → `Verbose`/`Debug`.

### 2.4 Existing test coverage

- [CliLoggingPolicyTests](tests/Cli.Tests/Configuration/CliLoggingPolicyTests.cs) — parses/emits matrix. Uses `error` as fallback default.
- [ConfigCliTests](tests/Cli.Tests/Configuration/ConfigCliTests.cs#L74) — asserts `"core.logging.level = error [default]"` in multiple places. Flipping the default requires updating these.
- [ConfigurationSchemaTests](tests/Cli.Tests/Configuration/ConfigurationSchemaTests.cs) — validates allowed values.

Docs to touch: [docs/howto/config.md](docs/howto/config.md#L11) and [docs/spec/config-implementation.md](docs/spec/config-implementation.md#L368).

---

## 3. Discussion points (what we want feedback on before coding)

### Q0. Default log level — decision recorded

**Decision (2026-07-21):** default `core.logging.level` moves from `"error"` → **`"warn"`**.

**Rationale (short form):**

- Azure-family peers converge on Warning-visible:
    - Azure CLI (Python) console default is `Warning`. Source: [`azlogging.py`](https://github.com/Azure/azure-cli/blob/dev/src/azure-cli-core/azure/cli/core/azlogging.py) header docstring.
    - Azure PowerShell effective baseline is `Warning + Error` (`$WarningPreference = Continue`, `$InformationPreference = SilentlyContinue`). Source: [about_preference_variables](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_preference_variables?view=powershell-7.6).
- AWS CLI is a counter-example (`Error`-only; no warn tier exists). Sibling-symmetry with Azure products wins for an Azure tool.
- Today `Warn` calls like [`LoginProcessor.cs` L419, L428](src/Extensions/Login/Processors/LoginProcessor.cs#L419-L428) ("No subscriptions found" / "Failed to auto-discover subscriptions") are silently swallowed under the `error` default. Flipping to `warn` surfaces them.
- `Info` remains hidden by default, so the call-site noise problem in §2.3 does not manifest. `info`-as-default is out of scope for this PR.

> **PS terminology aside:** the PowerShell docs quote about "Informational logging to the Operational channel" is about writing to the **OS event log / syslog** (telemetry-style), not console verbosity. Console verbosity in PS is governed by the [`$*Preference` variables](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_preference_variables?view=powershell-7.6). That's the apples-to-apples comparison used above.

### Q2. Stream routing: should non-output streams go to stderr?

**Proposal:** align CLI with Unix conventions.

| Stream             | Today  | Proposed   |
| ------------------ | ------ | ---------- |
| `WriteOutput`      | stdout | stdout     |
| `WriteError`       | stderr | stderr     |
| `WriteWarning`     | stdout | **stderr** |
| `WriteInformation` | stdout | **stderr** |
| `WriteVerbose`     | stdout | **stderr** |
| `WriteDebug`       | stdout | **stderr** |

Also touches:

- `ConsoleLoggingService` — the fallback logger. Route non-`Log`/non-`Info` (or all non-output) to stderr.
- `ConsoleOutputAdapter.WriteWarningAsync` — move to stderr.

**Concerns to raise:**

- **Test capture.** Test fixtures that read `ExecuteCli` output must capture both streams (or explicitly opt into stderr). Need to grep the [ExecuteCli helper](tests/Cli.Tests) surface before flipping.
- **`--output json`.** If a caller pipes `az foo --output json | jq …`, warnings/info currently pollute the JSON. Moving them to stderr **fixes** this — a clear win.
- **PowerShell cmdlets are unaffected** — they use `cmdlet.WriteVerbose`/`WriteWarning`/`WriteInformation` which route through PS's own stream model, not raw `Console.*`.
- **Colored output.** Warning/error use `Console.ForegroundColor`. Colors work on stderr but we should verify redirection behavior (`NO_COLOR`, `Console.IsErrorRedirected`).

### Q3. Scope of this PR

We should decide up-front how much to bite off. Three tiers:

1. **Investigate only** (this doc + issue comments, close with a decision). Low risk, no code.
2. **Stream routing fix** (Q2) without touching the default (Q1). This is the crisply-scoped, high-value fix — pipelines and JSON output stop being polluted.
3. **Full audit** — Q1 + Q2 + call-site severity re-triage + docs + test updates. Bigger blast radius.

**Recommendation to discuss:** ship **tier 2** as this PR, log **tier 3** (call-site audit + default change) as a follow-up issue with a checklist derived from §2.3.

### Q4. Are there call sites outside `ILoggingService` that should be brought in?

Uses of raw `Console.WriteLine` / `Console.Error.WriteLine` that bypass the policy:

- [DiscoverySession.cs L86, L114](src/Core/Core.Engine/DiscoverySession.cs#L86) — writes extension-load failures to stderr directly. Should this route through `ILoggingService` so it respects `core.logging.level`?
- [FileExporter.cs L95](src/Core/Core.Telemetry/FileExporter.cs#L95) — telemetry export error. Probably fine to stay raw (telemetry mustn't recurse).
- [CompletionGenerator.cs](src/CLI/Completions/CompletionGenerator.cs#L37) — writes completion JSON to stdout. **Correct as-is** — that IS the command output.
- [MsalAuthService device-code lines](src/Core/Core.Authentication/MsalAuthService.cs#L85-L86) — user-facing prompt during login. Correct as stdout? Or is that also an "informational" line that should honor `--output json`?

### Q5. Extension-load debug tracing — what to add? (new)

Today's coverage in [`src/CLI/AssemblyLoading/ExtensionLoader.cs`](src/CLI/AssemblyLoading/ExtensionLoader.cs):

| Event                                                                   | Logged today?     | Where                                                                                                                                                                     |
| ----------------------------------------------------------------------- | ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `LoadExtension(name, version)` entered                                  | ❌                | —                                                                                                                                                                         |
| Which loader is running (`Builtin` vs `MultiVersion`) and in what order | ❌                | —                                                                                                                                                                         |
| Candidate roots being probed                                            | ❌ (only on miss) | [L148](src/CLI/AssemblyLoading/ExtensionLoader.cs#L148), [L393](src/CLI/AssemblyLoading/ExtensionLoader.cs#L393)                                                          |
| Candidate directory names (`az-`, `Azclips.`, prefix, bare)             | ❌                | —                                                                                                                                                                         |
| Candidate assembly names tried                                          | ❌                | —                                                                                                                                                                         |
| File existence check per candidate                                      | ❌                | —                                                                                                                                                                         |
| Version selection in `MultiVersionExtensionLoader`                      | ❌                | —                                                                                                                                                                         |
| **Successful load** (which path, which version, which ALC)              | ❌                | —                                                                                                                                                                         |
| Per-dependency assembly resolve inside `ExtensionLoadContext.Load`      | ✅ (skip + hit)   | [L48, L54](src/CLI/AssemblyLoading/ExtensionLoader.cs#L48-L54)                                                                                                            |
| Extension not found — root(s) searched                                  | ✅                | [L148](src/CLI/AssemblyLoading/ExtensionLoader.cs#L148), [L393](src/CLI/AssemblyLoading/ExtensionLoader.cs#L393), [L403](src/CLI/AssemblyLoading/ExtensionLoader.cs#L403) |
| `.nupkg` metadata parse failure                                         | ✅                | [L221](src/CLI/AssemblyLoading/ExtensionLoader.cs#L221)                                                                                                                   |

Gaps we want to fill (all at `Debug` unless noted):

1. **`LoadExtension` entry point** — one line per call: `"[ExtLoader] Resolving extension '{name}' (version='{version ?? "any"}') via {loaderTypeName}."`
2. **Candidate probe log** — a single line summarizing the search space, or one line per candidate at `Debug`:
   `"[ExtLoader] Probing '{fullPath}' (root='{root}', dir='{directoryName}', asm='{assemblyName}')."`
3. **Hit log** — `"[ExtLoader] Loaded extension '{name}' v{version} from '{path}' into ALC '{alcName}'."` (at `Debug`; consider `Verbose` so it surfaces under `--verbose`).
4. **`MultiVersionExtensionLoader` version resolution** — log the enumerated candidate versions and the selected one before probing files.
5. **Loader-chain ordering** — one line at extension-resolution kickoff enumerating the registered `IExtensionLoader`s in order (helps explain "why did loader X win over Y").
6. **`ExtensionCandidateSelector` diagnostics** already exists ([used at CliExtensionServiceRegistration.cs L155](src/CLI/CliExtensionServiceRegistration.cs#L155)); confirm it fires on the CLI path too and not only the PS path.

Discussion:

- **Verbose vs Debug split.** "Which loader / which extension resolved" is useful under `--verbose`; per-candidate probe spam is `--debug` only. Recommendation: hit + loader-chain-order at `Verbose`, per-candidate probes at `Debug`.
- **Volume.** A cold `az` invocation can touch many candidate paths across many extensions. Guard per-candidate lines with `ShouldEmit(...)` cheaply (already how call sites work via `?.Debug`), and prefer one aggregated line per extension where possible.
- **PII / paths.** Paths under `%USERPROFILE%` / `$HOME` will appear in debug output. Consistent with existing behavior in this file, so no new concern — flag in PR description.
- **Interaction with §1.b item 3.** These are `Debug`-level messages; unless the user sets `--debug` or `core.logging.level=debug`, they never emit. So this item is **safe** to land alongside the stream-routing fix and the `warn` default flip.

---

## 4. Proposed plan of record (for review)

Tiering, given the `warn` default is committed (see §3 Q0):

**Tier B — stream routing fix (in this PR).**

1. **In `src/CLI/Program.cs BuildStreams`**, redirect `WriteWarning`, `WriteInformation`, `WriteVerbose`, `WriteDebug` to `Console.Error.WriteLine`. Keep `WriteOutput` on stdout. `WriteError` stays on stderr.
2. **In `src/Core/Core.Runtime/ConsoleLoggingService.cs`**, route `Warn`, `Verbose`, `Debug`, `Info`, `Error` to stderr; keep the `Log(...)` "raw" method on stdout (escape hatch for cmdlets/tools that need `Write-Host`-style output).
3. **In `src/Core/Core.Runtime/ConsoleOutputAdapter.cs`**, move `WriteWarningAsync` to stderr.

**Tier C — extension-load debug tracing (in this PR).**

4. Add the debug/verbose statements enumerated in Q5 to [`src/CLI/AssemblyLoading/ExtensionLoader.cs`](src/CLI/AssemblyLoading/ExtensionLoader.cs) covering `BuiltinExtensionLoader`, `MultiVersionExtensionLoader`, and the shared `ExtensionLoadContext`. All at `Debug` except the successful-load line (`Verbose`) and the loader-chain-order line (`Verbose`).
5. Also emit a one-line trace at loader-chain kickoff (where `IExtensionLoader`s are enumerated — likely in [`CliExtensionServiceRegistration.cs`](src/CLI/CliExtensionServiceRegistration.cs) or the composition root).

**Tier D — default-level flip to `warn` (in this PR).**

6. Change `LoggingOptions.Level` default in [CoreOptions.cs](src/Core/Core.Abstractions/CoreOptions.cs#L15-L18) from `"error"` → `"warn"`.
7. Change the schema mirror `DefaultValue` in [ConfigurationSchema.cs](src/Core/Core.Configuration/Schema/ConfigurationSchema.cs#L12) from `"error"` → `"warn"`. (`AllowedValues` already includes `"warn"`, so no schema surface change.)
8. Sync the two invalid/empty-value fallback branches in [`CliLoggingPolicy.Parse`](src/CLI/CliLoggingPolicy.cs#L38-L52) and [`CmdletOutputStreamsFactory.Parse`](src/PS/Common/CmdletOutputStreamsFactory.cs#L118-L132) from `LogLevel.Error` → `LogLevel.Warn`, so a malformed config value degrades to the new default rather than the old one.
9. **Do not** demote the `Info` call sites listed in §2.3 in this PR. Under `warn` they're already hidden by default, so the noise problem doesn't manifest and moving them is out of scope. Track as a follow-up if we ever aim at an `info` default.
10. Update [`ConfigCliTests`](tests/Cli.Tests/Configuration/ConfigCliTests.cs) assertions that hard-code `= error [default]` (see [L74, L108…](tests/Cli.Tests/Configuration/ConfigCliTests.cs) etc.) to expect `= warn [default]`.
11. Update [docs/howto/config.md](docs/howto/config.md#L11) and [docs/spec/config-implementation.md](docs/spec/config-implementation.md#L368) to document the new default; lift the short rationale from §3 Q0 into the docs verbatim.

**Tier E — tests & docs (in this PR).**

12. **Tests:**
    - Update `ExecuteCli` (and any assertion helpers) to capture stderr where needed.
    - Add a targeted test that asserts warning/info appear on **stderr**, not stdout, when running through `Program.Main`.
    - Add a test proving `az foo --output json 2>/dev/null` stays parseable when a warning fires mid-command.
    - Add a test that `az --debug <cmd>` emits the new extension-loader debug lines (grep for the `[ExtLoader]` / per-class prefix).
    - Add a test that verifies the new `warn` default: with no config set, warnings emit and info/debug do not.
13. **Docs:** append a short subsection to [docs/howto/config.md](docs/howto/config.md) documenting (a) the stream routing contract ("stdout is command result only") and (b) the new `warn` default with the short rationale from §3 Q0.

**Explicitly out of scope of this PR (deferred):**

- Demoting existing `Info` call sites to `Verbose`/`Debug` (see item 9 above).
- Reworking `ScopedLoggingService` / `OutputStreamAdapter` contract.
- PS cmdlet stream behavior.
- Introducing a `core.logging.stream` config knob (see Q6 in §6).
- Structured JSON logging.

---

## 5. Validation notes we'll want in the eventual PR

- `dotnet build azclips.slnx`
- `dotnet test tests/Cli.Tests/Cli.Tests.csproj`
- Manual: `az login --output json 2> /dev/null | jq .` and `az extension list 2> /dev/null | jq .` should produce clean JSON.
- Manual on Windows: verify `Console.ForegroundColor` still applies correctly when writing to stderr (both piped and non-piped).
- Verify `AZCLIPS_LOG_LEVEL=debug az …` still emits debug lines (now on stderr).
- Manual: `AZCLIPS_LOG_LEVEL=debug az <cmd-that-loads-extension>` should show the new extension-loader trace: entry → candidate probes → hit line (or the existing miss lines).
- Manual: `az --debug config get --key core.logging.level` should produce clean stdout output plus debug trace on stderr, still parseable when piped.
- Manual: `az config get --key core.logging.level` with no prior setting should print `core.logging.level = warn [default]` (previously `error`).
- Manual: an operation that emits `Warn` (e.g. login with no discoverable subscriptions — see [LoginProcessor.cs L419](src/Extensions/Login/Processors/LoginProcessor.cs#L419)) should surface the warning on stderr under the new default; previously the message was silently swallowed.

---

## 6. Open questions to close before coding

- [x] Do we ship the default-level change in this PR? **Yes — flip `error` → `warn`, backed by Azure CLI Python + Azure PowerShell citations (2026-07-21).**
- [ ] Do warnings need a `NO_COLOR` / redirection check before applying color when writing to stderr?
- [ ] Should `Log(...)` on `ILoggingService` stay on stdout (it's the "raw print" API), or is it also policy-controlled?
- [ ] Are there external consumers (agent renderer, TUI, PS host) that assume warnings arrive on stdout? Quick scan needed of `Core.Agent/Hosting/` and `TUI/`.
- [ ] Do we want a `core.logging.stream` config knob (`stdout` / `stderr` / `auto`) for users who need the old behavior back?
- [ ] Extension-load tracing: single aggregate line per extension, or one line per candidate? _(recommendation: aggregate at `Verbose`, per-candidate at `Debug`.)_
- [ ] Extension-load tracing prefix: use `[ExtLoader]` or per-class prefix like `[BuiltinExtensionLoader]`? Existing convention (e.g. [MsalAuthService](src/Core/Core.Authentication/MsalAuthService.cs#L36)) uses `[nameof(TypeName)]`. _(recommendation: per-class, matches existing convention.)_
