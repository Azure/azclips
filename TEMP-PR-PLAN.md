# TEMP-PR-PLAN — Issue #493

**Issue:** [Azure/CLIPS#493 — Logging: Examine default log-level, call site level usage, and Writeline vs Error.Writeline](https://github.com/Azure/CLIPS/issues/493)
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

3. **Change the default log level from `error` to `warn`, grounded in cross-CLI evidence.**
    - Decision as of 2026-07-21: baseline moves to **`warn`**. Rationale: Azure CLI (Python) console default is `Warning` (see [azlogging.py](https://github.com/Azure/azure-cli/blob/dev/src/azure-cli-core/azure/cli/core/azlogging.py)) and Azure PowerShell's effective visible baseline is `Warning + Error` (see [about_preference_variables](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_preference_variables?view=powershell-7.6)). Both sibling products converge on this level; today's `error` is stricter than either. See Q0.1 and Q1 below for details and citations.
    - The remaining CLIs in §4.1 still to be surveyed as a sanity check, but the decision does not hinge on them.

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

### Q0. What do other CLIs do? (evidence gathering — new)

We want the default-level and stream-routing choices to be **defensible from prior art**, not from taste. Before we commit, produce a short table (to be included in the PR body / this doc's §4.1) of the form:

> **Terminology sanity check (added 2026-07-21):** the quote from [about_logging_non-windows](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_logging_non-windows?view=powershell-7.6) that "PowerShell enables `Informational` logging to the `Operational` channel" describes PowerShell writing to the **OS event log / syslog / os_log Operational channel** — that is _telemetry-style logging to the operating system_, not user-facing console output. Console verbosity in PowerShell is controlled by the [`$*Preference` variables](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_preference_variables?view=powershell-7.6), which default to `SilentlyContinue` for Verbose / Debug / Information and `Continue` for Warning / Error. AzClips' `core.logging.level` is the console-verbosity knob, so the PS `$*Preference` defaults are the right comparison — not the Operational channel default.

### Q0.1 Findings so far (Azure CLI Python, Azure PowerShell)

**Azure CLI (Python `az`)** — primary source: [`src/azure-cli-core/azure/cli/core/azlogging.py`](https://github.com/Azure/azure-cli/blob/dev/src/azure-cli-core/azure/cli/core/azlogging.py) header docstring:

```
Level: Based on the verbosity option given by users, the logging levels for
root and CLI parent loggers are:
                       CLI Parent                  Root
                Console     File        Console     File
omitted         Warning     Debug       Critical    Debug
--verbose       Info        Debug       Critical    Debug
--debug         Debug       Debug       Debug       Debug
```

- **Console default = `Warning`** for the CLI logger; `--verbose` promotes to `Info`, `--debug` to `Debug`.
- Full `Debug` **always** goes to a rotating file under `~/.azure/logs/...` (independent of console level).
- Global flag [`--only-show-errors`](https://learn.microsoft.com/en-us/cli/azure/reference-index?view=azure-cli-latest#az-configure-optional-parameters) exists to suppress warnings — which only makes sense if warnings are shown by default.
- Python `logging.StreamHandler` writes to `sys.stderr` by default, so all log messages go to **stderr**; command output goes to stdout. Confirmed in the [troubleshooting doc](https://learn.microsoft.com/en-us/cli/azure/use-azure-cli-successfully-troubleshooting#the-debug-parameter) where `--debug` output is shown as a separate stream that can be redirected independently.

**Azure PowerShell (Az module)** — primary source: [`about_preference_variables`](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_preference_variables?view=powershell-7.6) default values, cross-referenced with the [Az troubleshooting page](https://learn.microsoft.com/en-us/powershell/azure/troubleshooting#enable-debug-logging).

| Preference variable      | Default value      | Effect on console output |
| ------------------------ | ------------------ | ------------------------ |
| `$ErrorActionPreference` | `Continue`         | Errors **shown**         |
| `$WarningPreference`     | `Continue`         | Warnings **shown**       |
| `$InformationPreference` | `SilentlyContinue` | Info **hidden**          |
| `$VerbosePreference`     | `SilentlyContinue` | Verbose **hidden**       |
| `$DebugPreference`       | `SilentlyContinue` | Debug **hidden**         |
| `$ProgressPreference`    | `Continue`         | Progress bar **shown**   |

- Effective visible baseline: **Error + Warning + Progress**, i.e. equivalent to a `warn` log level.
- `-Verbose` and `-Debug` common parameters opt in per-cmdlet; `$*Preference = 'Continue'` opts in globally.
- Az cmdlets writing warnings/errors/verbose/debug go through PS's stream model (not raw `Console.*`); when the streams are redirected with `2>` they land on stderr. Command result objects stay on stdout.
- Az explicitly [routes announcement messages through the Information stream](https://learn.microsoft.com/en-us/powershell/azure/troubleshooting#announcement-messages-in-automation-scenarios) so they "don't alter the returned object-based output" — a direct statement of the "stdout = data, other streams = human noise" contract.

**Convergence:** both siblings default to a **Warning** visibility baseline (Errors + Warnings visible; Info/Verbose/Debug opt-in via `--verbose`/`--debug` or `-Verbose`/`-Debug`). Neither defaults to `Error`-only, and neither defaults to `Info`.

### Q0.2 Survey table (remaining CLIs still to fill in)

| CLI                     | Default log level                                      | Non-output streams go to                    | Source (docs / code)                                                                                                                                                                                                                                       |
| ----------------------- | ------------------------------------------------------ | ------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Azure CLI (Python `az`) | **Warning** (console); Debug (file)                    | **stderr** (Python `StreamHandler` default) | [azlogging.py](https://github.com/Azure/azure-cli/blob/dev/src/azure-cli-core/azure/cli/core/azlogging.py) header; [troubleshooting doc](https://learn.microsoft.com/en-us/cli/azure/use-azure-cli-successfully-troubleshooting#the-debug-parameter)       |
| Azure PowerShell (`Az`) | **Warning + Error visible**; Info/Verbose/Debug hidden | PS streams (stderr under `2>`)              | [about_preference_variables](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_preference_variables?view=powershell-7.6); [Az troubleshooting](https://learn.microsoft.com/en-us/powershell/azure/troubleshooting) |
| GitHub CLI (`gh`)       | TBD                                                    | TBD                                         | TBD                                                                                                                                                                                                                                                        |
| `kubectl`               | TBD                                                    | TBD                                         | TBD                                                                                                                                                                                                                                                        |
| `docker`                | TBD                                                    | TBD                                         | TBD                                                                                                                                                                                                                                                        |
| `terraform`             | TBD                                                    | TBD                                         | TBD                                                                                                                                                                                                                                                        |
| AWS CLI (`aws`)         | TBD                                                    | TBD                                         | TBD                                                                                                                                                                                                                                                        |
| `gcloud`                | TBD                                                    | TBD                                         | TBD                                                                                                                                                                                                                                                        |
| `git`                   | (n/a — porcelain)                                      | stderr                                      | git-porcelain docs                                                                                                                                                                                                                                         |

Rules for the remaining rows:

- Prefer **published docs** (man page, `--help`, official reference) over inferred behavior. Cite URLs.
- Where relevant, note the **verbosity flag convention** (`-v`, `--verbose`, `--debug`, `-vvv`, `--log-level`).
- Note whether the CLI **routes JSON output** cleanly regardless of verbosity — i.e. does `<cmd> --output json 2>/dev/null | jq .` work?
- Note any explicit "stdout = machine-readable, stderr = human diagnostics" statement.

**Deliverable:** the completed table becomes the _justification_ section of the PR description. Whatever default we pick for AzClips (`error`, `warn`, `info`), we cite ≥2 comparable CLIs that use the same choice. **Current leader based on Q0.1: `warn`** — cited peers Azure CLI Python and Azure PowerShell.

### Q1. Default level: `error`, `warn`, or `info`?

**Update (2026-07-21):** Q0.1 evidence points to `warn` as the sibling-consistent baseline. Rewriting the arguments below with that in mind.

**Arguments for keeping `error` (status quo from PR #313):**

- Existing call sites are noisy at `Info` (see §2.3) — raising the default without triaging them regresses UX.
- Zero risk of unexpected warnings surfacing to users.

**Arguments for moving to `warn` (recommended, matches Azure CLI + Azure PowerShell):**

- Azure CLI (Python) console default = `Warning`. See [azlogging.py](https://github.com/Azure/azure-cli/blob/dev/src/azure-cli-core/azure/cli/core/azlogging.py).
- Azure PowerShell effective default = `Warning + Error` visible (`$WarningPreference = Continue`, `$InformationPreference = SilentlyContinue`). See [about_preference_variables](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_preference_variables).
- Warnings **should** reach users — e.g. LoginProcessor's "No subscriptions found" and "Failed to auto-discover subscriptions" ([L419, L428](src/Extensions/Login/Processors/LoginProcessor.cs#L419-L428)) are currently silenced by default under `error`. That is a bug the current default is masking.
- Symmetry: users switching between `az` (Python) and AzClips shouldn't see a strictly quieter tool.

**Arguments for moving all the way to `info`:**

- Only holds up if we first demote the `Info` call sites in §2.3 to `Verbose`/`Debug`. Otherwise `info` regresses UX badly.
- The PS quote about "Informational logging to the Operational channel" is about the OS event log, **not** console verbosity (see Q0 note). It is not evidence that CLIs default to Info on the console.
- No cited peer defaults its **console** to Info. Rejected as the default for this PR.

**Recommendation to discuss:** move default from `error` → `warn` in this PR (Tier D of §4). Two cited peers back it. Warnings that were silently swallowed at `error` now surface; existing `Info` call sites remain hidden by default so the noise problem in §2.3 does not manifest. If we later decide `info` is worth aiming at, land the call-site retriage first as a separate PR.

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
- **Interaction with §1.b item 3.** These are `Debug`-level messages; unless the user sets `--debug` or `core.logging.level=debug`, they never emit. So this item is **safe** to land alongside the stream-routing fix regardless of what the survey concludes.

---

## 4. Proposed plan of record (for review)

Revised tiering after adding items #3 and #4 from §1.b, and committing to `warn` as the new default (2026-07-21):

**Tier A — evidence & citation (must land in this PR).**

1. **Cross-CLI survey** (Q0). Azure CLI Python and Azure PowerShell rows are already cited in §4.1 and support `warn`. Fill in the remaining rows (`gh`, `kubectl`, `docker`, `terraform`, `aws`, `gcloud`, `git`) as a sanity check and include the completed table in the PR description. Decision does **not** hinge on them.

**Tier B — stream routing fix (in this PR).**

2. **In `src/CLI/Program.cs BuildStreams`**, redirect `WriteWarning`, `WriteInformation`, `WriteVerbose`, `WriteDebug` to `Console.Error.WriteLine`. Keep `WriteOutput` on stdout. `WriteError` stays on stderr.
3. **In `src/Core/Core.Runtime/ConsoleLoggingService.cs`**, route `Warn`, `Verbose`, `Debug`, `Info`, `Error` to stderr; keep the `Log(...)` "raw" method on stdout (escape hatch for cmdlets/tools that need `Write-Host`-style output).
4. **In `src/Core/Core.Runtime/ConsoleOutputAdapter.cs`**, move `WriteWarningAsync` to stderr.

**Tier C — extension-load debug tracing (in this PR).**

5. Add the debug/verbose statements enumerated in Q5 to [`src/CLI/AssemblyLoading/ExtensionLoader.cs`](src/CLI/AssemblyLoading/ExtensionLoader.cs) covering `BuiltinExtensionLoader`, `MultiVersionExtensionLoader`, and the shared `ExtensionLoadContext`. All at `Debug` except the successful-load line (`Verbose`) and the loader-chain-order line (`Verbose`).
6. Also emit a one-line trace at loader-chain kickoff (where `IExtensionLoader`s are enumerated — likely in [`CliExtensionServiceRegistration.cs`](src/CLI/CliExtensionServiceRegistration.cs) or the composition root).

**Tier D — default-level flip to `warn` (in this PR).**

7. Change `LoggingOptions.Level` default in [CoreOptions.cs](src/Core/Core.Abstractions/CoreOptions.cs#L15-L18) from `"error"` → `"warn"`.
8. Change the schema mirror `DefaultValue` in [ConfigurationSchema.cs](src/Core/Core.Configuration/Schema/ConfigurationSchema.cs#L12) from `"error"` → `"warn"`. (`AllowedValues` already includes `"warn"`, so no schema surface change.)
9. Sync the two invalid/empty-value fallback branches in [`CliLoggingPolicy.Parse`](src/CLI/CliLoggingPolicy.cs#L38-L52) and [`CmdletOutputStreamsFactory.Parse`](src/PS/Common/CmdletOutputStreamsFactory.cs#L118-L132) from `LogLevel.Error` → `LogLevel.Warn`, so a malformed config value degrades to the new default rather than the old one.
10. **Do not** demote the `Info` call sites listed in §2.3 in this PR. Under `warn` they're already hidden by default, so the noise problem doesn't manifest and moving them is out of scope. Track as a follow-up if we ever aim at an `info` default.
11. Update [`ConfigCliTests`](tests/Cli.Tests/Configuration/ConfigCliTests.cs) assertions that hard-code `= error [default]` (see [L74, L108…](tests/Cli.Tests/Configuration/ConfigCliTests.cs) etc.) to expect `= warn [default]`.
12. Update [docs/howto/config.md](docs/howto/config.md#L11) and [docs/spec/config-implementation.md](docs/spec/config-implementation.md#L368) to document the new default and the rationale (link back to §4.1).

**Tier E — tests & docs (in this PR).**

13. **Tests:**
    - Update `ExecuteCli` (and any assertion helpers) to capture stderr where needed.
    - Add a targeted test that asserts warning/info appear on **stderr**, not stdout, when running through `Program.Main`.
    - Add a test proving `az foo --output json 2>/dev/null` stays parseable when a warning fires mid-command.
    - Add a test that `az --debug <cmd>` emits the new extension-loader debug lines (grep for the `[ExtLoader]` / per-class prefix).
    - Add a test that verifies the new `warn` default: with no config set, warnings emit and info/debug do not.
14. **Docs:** append a short subsection to [docs/howto/config.md](docs/howto/config.md) documenting (a) the stream routing contract ("stdout is command result only") and (b) the new `warn` default with a link back to the cross-CLI table in §4.1.

**Explicitly out of scope of this PR (deferred):**

- Demoting existing `Info` call sites to `Verbose`/`Debug` (see item 10 above).
- Reworking `ScopedLoggingService` / `OutputStreamAdapter` contract.
- PS cmdlet stream behavior.
- Introducing a `core.logging.stream` config knob (see Q6 in §6).
- Structured JSON logging.

### 4.1 Cross-CLI defaults table (partially populated — see Q0.2)

> Rows for Azure CLI Python and Azure PowerShell are cited in Q0.1/Q0.2. Remaining rows to be filled by the Tier A survey.

| CLI                     | Default level                       | Non-output streams             | Verbosity flag(s)                            | Source                                                                                                                                       |
| ----------------------- | ----------------------------------- | ------------------------------ | -------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| Azure CLI (Python `az`) | **Warning** (console); Debug (file) | **stderr**                     | `--verbose`, `--debug`, `--only-show-errors` | [azlogging.py](https://github.com/Azure/azure-cli/blob/dev/src/azure-cli-core/azure/cli/core/azlogging.py)                                   |
| Azure PowerShell (`Az`) | **Warning + Error** visible         | PS streams (stderr under `2>`) | `-Verbose`, `-Debug`, `-InformationAction`   | [about_preference_variables](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_preference_variables) |
| GitHub CLI (`gh`)       | TBD                                 | TBD                            | `GH_DEBUG=api`                               | TBD                                                                                                                                          |
| `kubectl`               | TBD                                 | TBD                            | `-v=N` (klog verbosity)                      | TBD                                                                                                                                          |
| `docker`                | TBD                                 | TBD                            | `--log-level`                                | TBD                                                                                                                                          |
| `terraform`             | TBD                                 | TBD                            | `TF_LOG`, `TF_LOG_LEVEL`                     | TBD                                                                                                                                          |
| AWS CLI (`aws`)         | TBD                                 | TBD                            | `--debug`                                    | TBD                                                                                                                                          |
| `gcloud`                | TBD                                 | TBD                            | `--verbosity`                                | TBD                                                                                                                                          |
| `git`                   | (porcelain)                         | stderr                         | `GIT_TRACE`, `-v`                            | TBD                                                                                                                                          |

**AzClips choice (committed 2026-07-21):** **`warn`** — cited peers: Azure CLI Python, Azure PowerShell.

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
