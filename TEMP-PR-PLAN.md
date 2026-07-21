# TEMP-PR-PLAN — Issue #493

**Issue:** [Azure/CLIPS#493 — Logging: Examine default log-level, call site level usage, and Writeline vs Error.Writeline](https://github.com/Azure/CLIPS/issues/493)
**Draft PR:** [Azure/azclips#575 — {Core} Update default log-level to `WARN`, adjust call sites, and add debug logging for extension loading](https://github.com/Azure/azclips/pull/575)
**Branch:** `clips-493-default-log-level-call-sites`
**Milestone:** AzCLIPS: July 5 – July 18
**Squad:** act-quality-productivity-squad

---

## 1. Summary

Three intertwined changes to CLI logging, all shipping in PR #575:

1. **Stream routing fix** — align with POSIX/GNU: stdout = command data, stderr = diagnostics. Today [`BuildStreams`](src/CLI/Program.cs#L203-L253) sends warning/info/verbose/debug to stdout, polluting `--output json` pipelines. Every peer CLI (Azure CLI Python, Az PowerShell, AWS CLI, git, kubectl, docker, terraform) puts diagnostics on stderr.
2. **Default log level → `warn`** (was `error`). Matches Azure CLI Python and Az PowerShell defaults; surfaces warnings that are currently silently swallowed (e.g. [`LoginProcessor.cs` L419, L428](src/Extensions/Login/Processors/LoginProcessor.cs#L419-L428)).
3. **Extension-load debug tracing** — per-extension load rows + aggregate summary table on [`ExtensionLoader`](src/CLI/AssemblyLoading/ExtensionLoader.cs), modelled on [`azure-cli/src/azure-cli-core/azure/cli/core/__init__.py` L232-L235, L680-L688](https://github.com/Azure/azure-cli/blob/dev/src/azure-cli-core/azure/cli/core/__init__.py#L232). All at `Debug`; emitted only under `--debug` or `core.logging.level=debug`.

---

## 2. Decision Registry

Succinct "why" for each committed decision. All resolved 2026-07-21.

| #      | Decision                                                                     | Rationale                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| ------ | ---------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **D1** | Default log level: `warn` (was `error`)                                      | Azure-family peer symmetry. Azure CLI Python console default is `Warning` ([azlogging.py](https://github.com/Azure/azure-cli/blob/dev/src/azure-cli-core/azure/cli/core/azlogging.py) header docstring); Az PowerShell effective baseline is `Warning + Error` ([about_preference_variables](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_preference_variables?view=powershell-7.6)). AWS CLI is `Error`-only but has no warn tier at all — different data model, not a competing default. `info`-as-default rejected: no cited peer defaults console to Info, and would require call-site retriage (see D8). |
| **D2** | Route Warning/Info/Verbose/Debug to **stderr**                               | Industry standard. POSIX defines stderr as the "diagnostic output stream". [GNU Coding Standards §4.4](https://www.gnu.org/prep/standards/html_node/Errors.html): _"All error messages should be directed to stderr rather than stdout."_ Adopted by every peer CLI examined. Current stdout routing is a bug, not a philosophy.                                                                                                                                                                                                                                                                                                                           |
| **D3** | Collapse `ILoggingService.Log(...)` → `Info(...)` in `ConsoleLoggingService` | Zero production call sites (grep confirmed; only reference is [`RendererLoggingServiceTests.cs` L67](Agent/tests/Core.Agent.Tests/RendererLoggingServiceTests.cs#L67) which iterates the interface for coverage). [`OutputStreamAdapter.Log`](src/Core/Core.Runtime/ScopedLoggingService.cs#L66-L69) already treats it as Info. Restores the invariant "no `ILoggingService` method writes to stdout." Full interface deprecation tracked as FU.                                                                                                                                                                                                           |
| **D4** | Add `ShouldEmitColor()` guard with standard precedence                       | Precedence chain matches ecosystem standard: `NO_COLOR` ([no-color.org](https://no-color.org)) → `FORCE_COLOR` → `TERM=dumb` → `Console.IsErrorRedirected` → else color. Adopted by git, npm, cargo, terraform, etc. Applied only at the two existing color sites; broader color audit deferred to FU.                                                                                                                                                                                                                                                                                                                                                     |
| **D5** | Extension-load tracing: per-load rows + aggregate table, all at `Debug`      | Copies pattern from [`azure-cli __init__.py` L232-L235, L680-L688](https://github.com/Azure/azure-cli/blob/dev/src/azure-cli-core/azure/cli/core/__init__.py#L232): class-level format-string constants, per-load debug row via `logger.debug(item_format_string, mod, elapsed_time, ...)`, `"Total ({count})"` summary row at end. Uniform `Debug` level keeps the code path simple; user gets everything or nothing based on `--debug`.                                                                                                                                                                                                                  |
| **D6** | Log prefix: per-class `[nameof(TypeName)]`                                   | Matches existing convention in the codebase — e.g. [`MsalAuthService`](src/Core/Core.Authentication/MsalAuthService.cs#L36). Consistent with what future greppers will expect.                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| **D7** | Fix TUI's stderr classifier as part of Tier B                                | External-consumer scan found [`TUI/services/ProcessService.RunCliAsync` L28-L67](TUI/services/ProcessService.cs#L28-L67) maps all stderr → `HistoryItemType.Error`. Without this fix, every warning post-D2 would render as an error in the TUI — a real UX regression. Fix: extend [`ClassifyLine`](TUI/services/ProcessService.cs#L263-L271) to recognize the AzClips prefix set (`WARNING:`, `INFO:`, `[ERROR]`, `VERBOSE:`, `DEBUG:`).                                                                                                                                                                                                                 |
| **D8** | Don't demote `Info` call sites in this PR                                    | Under `warn` the `Info` sites (see [CLIArgumentParsingProvider.cs L23](src/CLI/ArgumentParsing/CLIArgumentParsingProvider.cs#L23), [Engine.Subscriptions.cs L124-L149](src/Core/Core.Engine/Engine.Subscriptions.cs#L124-L149), [TimingActivityListener.cs L52](src/Core/Core.Telemetry/TimingActivityListener.cs#L52)) stay hidden by default, so the noise problem does not manifest. Retriage only becomes worthwhile under an eventual `info` default; tracked as FU.                                                                                                                                                                                  |
| **D9** | New subprocess-based integration test required                               | Existing [`ExecuteCli` fixture](tests/Cli.Tests/Configuration/ConfigCliTests.cs#L362) constructs its own `OutputStreams` and bypasses `Program.Main` / `BuildStreams` entirely — cannot test D2 routing. Model new test on [`AgentToolsLiveTests.RunAsync`](Agent/tests/Core.Agent.Tests/AgentToolsLiveTests.cs#L206) which captures stdout+stderr separately.                                                                                                                                                                                                                                                                                             |

**PS terminology aside** (relevant to D1): the [about_logging_non-windows](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_logging_non-windows?view=powershell-7.6) quote about _"Informational logging to the Operational channel"_ describes PowerShell writing to the **OS event log / syslog** (telemetry-style), not console verbosity. Console verbosity in PS is governed by the `$*Preference` variables, which is the apples-to-apples comparison used in D1.

---

## 3. Implementation plan

### 3.1 Stream routing (D2, D3, D4, D7)

1. ✅ [`src/CLI/Program.cs` `BuildStreams` L203-L253](src/CLI/Program.cs#L203-L253) — redirect `WriteWarning`, `WriteInformation`, `WriteVerbose`, `WriteDebug` to `Console.Error.WriteLine`. `WriteOutput` stays on stdout; `WriteError` stays on stderr. _(commit `74ca5521a`)_
2. ✅ [`src/Core/Core.Runtime/ConsoleLoggingService.cs`](src/Core/Core.Runtime/ConsoleLoggingService.cs) — route `Warn`, `Verbose`, `Debug`, `Info`, `Error` to stderr. Collapse `Log(...)` → `Info(...)`. _(commit `9d07e9e7b`)_
3. [`src/Core/Core.Runtime/ConsoleOutputAdapter.cs`](src/Core/Core.Runtime/ConsoleOutputAdapter.cs) — move `WriteWarningAsync` to stderr.
4. Add a `ShouldEmitColor()` helper in `Program.cs` (or a small `ColorPolicy` static) with precedence `NO_COLOR` → `FORCE_COLOR` → `TERM=dumb` → `Console.IsErrorRedirected` → else color. Guard the two existing color sites (`WriteError` in `BuildStreams`, colored paths in `ConsoleLoggingService`). ~15 lines total.
5. [`TUI/services/ProcessService.cs`](TUI/services/ProcessService.cs) — extend [`ClassifyLine` L263-L271](TUI/services/ProcessService.cs#L263-L271) to recognize `WARNING:`, `INFO:`, `[ERROR]`, `VERBOSE:`, `DEBUG:` prefixes on stderr and classify accordingly. [`RunLocalAsync` L124](TUI/services/ProcessService.cs#L124) already handles the general "tools write info to stderr" pattern; this brings `RunCliAsync` in line.

### 3.2 Default log level flip to `warn` (D1)

6. [`src/Core/Core.Abstractions/CoreOptions.cs` L15-L18](src/Core/Core.Abstractions/CoreOptions.cs#L15-L18) — `LoggingOptions.Level` default `"error"` → `"warn"`.
7. [`src/Core/Core.Configuration/Schema/ConfigurationSchema.cs` L12](src/Core/Core.Configuration/Schema/ConfigurationSchema.cs#L12) — schema mirror `DefaultValue` `"error"` → `"warn"`. `AllowedValues` unchanged (already includes `"warn"`).
8. [`src/CLI/CliLoggingPolicy.cs` L38-L52](src/CLI/CliLoggingPolicy.cs#L38-L52) and [`src/PS/Common/CmdletOutputStreamsFactory.cs` L118-L132](src/PS/Common/CmdletOutputStreamsFactory.cs#L118-L132) — sync invalid/empty-value fallback from `LogLevel.Error` → `LogLevel.Warn`, so a malformed config value degrades to the new default rather than the old one.
9. [`tests/Cli.Tests/Configuration/ConfigCliTests.cs`](tests/Cli.Tests/Configuration/ConfigCliTests.cs) — update assertions hard-coding `= error [default]` (see L74, L108) to `= warn [default]`.

### 3.3 Extension-load debug tracing (D5, D6)

10. Class-level format constants on `BuiltinExtensionLoader` / `MultiVersionExtensionLoader` (or a shared static class):

    ```csharp
    // Aligned columns: Name (30), Load Time ms (10), Version (12), Path
    private const string ExtLoadHeader = "[{0}] {1,-30} {2,10} {3,-12} {4}";
    private const string ExtLoadRow    = "[{0}] {1,-30} {2,10:F1} {3,-12} {4}";
    ```

11. `ExtensionLoadTracker` — small AsyncLocal-scoped class following the pattern in [`ScopedLoggingService`](src/Core/Core.Runtime/ScopedLoggingService.cs). API: `Record(name, elapsedMs, version, path)` and `EmitSummary(ILoggingService)`.

12. [`src/CLI/AssemblyLoading/ExtensionLoader.cs`](src/CLI/AssemblyLoading/ExtensionLoader.cs) — add these `Debug` lines from `BuiltinExtensionLoader`, `MultiVersionExtensionLoader`, and `ExtensionLoadContext`:

    | Event                            | Format                                                                                                        |
    | -------------------------------- | ------------------------------------------------------------------------------------------------------------- |
    | `LoadExtension` entry            | `[{loaderTypeName}] Resolving extension '{name}' (version='{version ?? "any"}').`                             |
    | Per-candidate probe              | `[{loaderTypeName}] Probing '{fullPath}' (root='{root}', dir='{dir}', asm='{asm}').`                          |
    | Successful load                  | Recorded into `ExtensionLoadTracker` and logged via `ExtLoadRow`.                                             |
    | Version selection (MultiVersion) | `[MultiVersionExtensionLoader] Version selection for '{name}': candidates=[{v1},...], selected='{selected}'.` |

13. [`src/CLI/CliExtensionServiceRegistration.cs`](src/CLI/CliExtensionServiceRegistration.cs) — emit loader-chain kickoff line: `[ExtensionResolution] Loader chain: [BuiltinExtensionLoader, MultiVersionExtensionLoader, ...].`

14. [`src/CLI/Program.cs`](src/CLI/Program.cs) `Main` `finally` — dump `ExtensionLoadTracker.EmitSummary(...)`. Expected output:

    ```
    [ExtensionLoadTracker] Extension                      Time (ms) Version      Path
    [ExtensionLoadTracker] az-account                          45.2 v3.0.1       C:\...\account.dll
    [ExtensionLoadTracker] az-storage                         182.7 v4.1.0       C:\...\storage.dll
    [ExtensionLoadTracker] Total (2)                          227.9 -            -
    ```

15. Confirm [`ExtensionCandidateSelector`](src/CLI/CliExtensionServiceRegistration.cs#L155) diagnostics fire on the CLI path too, not just the PS path.

### 3.4 Tests (D9)

16. New subprocess-based integration test in `tests/Cli.Tests/` — shells out to the built `az` binary and captures stdout+stderr separately (model on [`AgentToolsLiveTests.RunAsync`](Agent/tests/Core.Agent.Tests/AgentToolsLiveTests.cs#L206) or [`ManagedIdentityE2ETests.RunCliAsync`](tests/Cli.Tests/ManagedIdentityE2ETests.cs#L116)). Asserts:
    - Warning/info lines appear on **stderr**; command result on stdout.
    - `az foo --output json 2>/dev/null` stays parseable when a warning fires mid-command.
    - `az --debug <cmd>` emits extension-loader debug lines on stderr (grep for `[BuiltinExtensionLoader]` / `[MultiVersionExtensionLoader]` prefix).
    - With no config set + an operation that emits `Warn`, warning surfaces on stderr; `Info`/`Debug` do not.
17. `TUI` unit test for the extended `ClassifyLine` (item 5) covering each prefix.

### 3.5 Docs

18. [docs/howto/config.md L11](docs/howto/config.md#L11) and [docs/spec/config-implementation.md L368](docs/spec/config-implementation.md#L368) — document (a) the stream routing contract ("stdout is command result only") and (b) the new `warn` default. Lift rationale from §2 D1/D2.

### Explicitly out of scope

- Demoting existing `Info` call sites (D8; tracked as FU).
- Reworking `ScopedLoggingService` / `OutputStreamAdapter` contract.
- PS cmdlet stream behavior.
- `core.logging.stream` config knob.
- Structured JSON logging.
- Broader color audit / `ColorPolicy` abstraction.

---

## 4. Validation

- `dotnet build azclips.slnx`
- `dotnet test tests/Cli.Tests/Cli.Tests.csproj`
- Manual: `az login --output json 2> /dev/null | jq .` and `az extension list 2> /dev/null | jq .` produce clean JSON.
- Manual (Windows): `Console.ForegroundColor` behavior on stderr, both piped and non-piped; `NO_COLOR=1 az foo` suppresses color.
- Manual: `AZCLIPS_LOG_LEVEL=debug az <cmd-that-loads-extension>` shows the new tracker summary and per-extension rows.
- Manual: `az --debug config get --key core.logging.level` — clean stdout, debug trace on stderr, still parseable when piped.
- Manual: `az config get --key core.logging.level` with no prior setting prints `core.logging.level = warn [default]` (previously `error`).
- Manual: an operation that emits `Warn` (e.g. login with no discoverable subscriptions — [`LoginProcessor.cs` L419](src/Extensions/Login/Processors/LoginProcessor.cs#L419)) surfaces the warning on stderr under the new default; previously silently swallowed.

---

## 5. Follow-ups (out of this PR)

Separate issues / PRs to open once this one lands. Captured so we don't lose them.

- [ ] **Broader color audit / `ColorPolicy` abstraction.** This PR adds a minimal `ShouldEmitColor()` helper (§3.1 item 4) applied only to the two current color-using sites. Follow-up should audit color usage across `TUI/`, `Core.Agent/Hosting/`, and any other `Console.ForegroundColor` sites, and consider lifting into a shared `ColorPolicy` service or adopting [Spectre.Console](https://spectreconsole.net/) wholesale.

- [ ] **Deprecate and remove `ILoggingService.Log(string)`.** `Log` has zero production call sites (grep confirmed 2026-07-21; only reference is [`RendererLoggingServiceTests.cs` L67](Agent/tests/Core.Agent.Tests/RendererLoggingServiceTests.cs#L67)) and its semantics duplicate `Info` after D3. Removing it is a public-API break, so it needs its own PR with a proper deprecation path:
    1. Mark `Log` with `[Obsolete("Use Info instead. Log will be removed in a future release.", error: false)]` in [ILoggingService.cs](src/Core/Core.Abstractions/ILoggingService.cs#L14-L15).
    2. Remove the coverage test at [`RendererLoggingServiceTests.cs` L67](Agent/tests/Core.Agent.Tests/RendererLoggingServiceTests.cs#L67) or narrow it to only the levelled methods.
    3. In a later release, remove `Log` from the interface and from `ConsoleLoggingService`, `ScopedLoggingService`, `OutputStreamAdapter`, `NoOpLoggingService`, and `RendererLoggingService`.

- [ ] **Call-site severity retriage under an `info` default** (D8). Only worthwhile if we ever aim at `core.logging.level = info` as the baseline; would require demoting the `Info` sites (alias resolution, extension-load progress, `[TIMING]` traces, subscription lifecycle) to `Verbose`/`Debug` first.

- [ ] **`core.logging.stream` config knob** (`stdout` / `stderr` / `auto`). Useful only if users report a real need to revert to the pre-D2 stdout-for-everything behavior. Bug-report-driven; open only if warranted.

- [ ] **Structured JSON logging** (one JSON object per log line, machine-parseable for CI). Bigger design question; keeps `--output json` clean for a different reason than D2. Not in scope for this PR.

- [ ] **Rework of `ScopedLoggingService` / `OutputStreamAdapter` contract.** Only needed if the async-flow semantics prove problematic in practice.

- [ ] **Consolidate console-writing primitives into a single `ConsoleSink`.** Observation from §3.1 implementation (2026-07-21): three surfaces \u2014 [`BuildStreams`](src/CLI/Program.cs#L203-L253), [`ConsoleLoggingService`](src/Core/Core.Runtime/ConsoleLoggingService.cs), and [`ConsoleOutputAdapter`](src/Core/Core.Runtime/ConsoleOutputAdapter.cs) \u2014 each independently pick a prefix, a color, a stream, and a level gate. Prefixes even drift (`[INFO]` vs `INFO:`, `[WARN]` vs `WARNING:`) between surfaces. After §3.1 item 4 lands, `ShouldEmitColor()` becomes yet another duplicated concern. Follow-up: introduce a single `ConsoleSink.Emit(level, message)` primitive that owns prefix + color + stream + level-gating; each of the three surfaces then calls it instead of writing to `Console.*` directly. Keeps the roles distinct (per-invocation `OutputStreams`, DI-injected `ILoggingService`, interactive `IOutputAdapter`) but factors out the shared rendering. Out of scope for the current PR to keep the routing-fix diff focused and reviewable.

- [ ] **Bring stray raw `Console.*` call sites under `ILoggingService`** where appropriate. Candidates identified during scan: [`DiscoverySession.cs` L86, L114](src/Core/Core.Engine/DiscoverySession.cs#L86) writes extension-load failures to stderr directly and should route through `ILoggingService` so it respects `core.logging.level`. [`FileExporter.cs` L95](src/Core/Core.Telemetry/FileExporter.cs#L95) (telemetry) and [`CompletionGenerator.cs`](src/CLI/Completions/CompletionGenerator.cs#L37) (JSON completion output) should stay raw.

- [ ] **PS cmdlet stream behavior** post-merge review. Explicitly untouched by this PR. Confirm no regressions; open a follow-up if the PS host surface needs alignment with the CLI's new stderr routing.
