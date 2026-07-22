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

| #       | Decision                                                                                     | Rationale                                                                                                                                                                                                                                                                                                                                              |
| ------- | -------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **D1**  | Default log level: `warn` (was `error`)                                                      | Azure-family peer symmetry — Azure CLI Python defaults to `Warning`; Az PowerShell effective baseline is `Warning + Error`. `info`-as-default rejected (no cited peer default; would need call-site retriage). Shared config also flips PS cmdlets; `-WarningAction Ignore` is the escape hatch.                                                       |
| **D2**  | Route Warning/Info/Verbose/Debug to **stderr**                                               | Industry standard. POSIX defines stderr as the diagnostic stream; [GNU Coding Standards §4.4](https://www.gnu.org/prep/standards/html_node/Errors.html) codifies it; every peer CLI examined follows it. Current stdout routing is a bug.                                                                                                              |
| **D3**  | Collapse `ILoggingService.Log(...)` → `Info(...)` in `ConsoleLoggingService`                 | Zero production call sites; [`OutputStreamAdapter.Log`](src/Core/Core.Runtime/ScopedLoggingService.cs#L66-L69) already delegated to `Info`. `Log` stays on the interface as a permanent alias — no deprecation planned.                                                                                                                               |
| **D4**  | Add `ShouldEmitColor()` guard with standard precedence                                       | Precedence chain matches ecosystem standard: `NO_COLOR` → `FORCE_COLOR` → `TERM=dumb` → redirection check → color. Applied at all eight color sites; broader audit rolled into the [`ConsoleSink` follow-up](https://github.com/Azure/CLIPS/issues/543).                                                                                        |
| **D5**  | Extension-load tracing: per-load rows + aggregate table, all at `Debug`                      | Copies the [azure-cli `__init__.py`](https://github.com/Azure/azure-cli/blob/dev/src/azure-cli-core/azure/cli/core/__init__.py#L232) L232-L235, L680-L688 pattern: format-string constants + per-load Debug row + `Total (count)` summary. Uniform `Debug` level — user gets everything or nothing under `--debug`.                                    |
| **D6**  | Log prefix: per-class `[nameof(TypeName)]`                                                   | Matches existing codebase convention — e.g. [`MsalAuthService`](src/Core/Core.Authentication/MsalAuthService.cs#L36).                                                                                                                                                                                                                                 |
| **D7**  | Fix TUI's stderr classifier as part of Tier B                                                | [`TUI ProcessService.RunCliAsync`](TUI/services/ProcessService.cs) mapped all stderr → `HistoryItemType.Error`; post-D2 every warning would render as an error in the TUI. `ClassifyLine` extended to recognize the AzClips prefix set.                                                                                                               |
| **D8**  | Don't demote `Info` call sites in this PR                                                    | Under `warn`, `Info` sites stay hidden by default so noise doesn't manifest. No `info`-default move planned; revisit only if that ever changes.                                                                                                                                                                                                        |
| **D9**  | New subprocess-based integration test required                                               | [`ExecuteCli` fixture](tests/Cli.Tests/Configuration/ConfigCliTests.cs#L362) constructs its own `OutputStreams` and bypasses `Program.Main` / `BuildStreams` — cannot test D2 routing.                                                                                                                                                                |
| **D10** | Harmonize diagnostic prefix to `LEVEL: message` (uppercase word, single colon, single space) | Three surfaces were emitting three different prefix styles. Chose `LEVEL:` over `[LEVEL]`: matches Azure CLI Python's output, the Python `logging` default formatter, and the PS host's own rendering of `cmdlet.WriteWarning(...)`. Consolidated pre-production while no downstream parser has locked in a style.                                     |

---

## 3. Implementation summary

All 18 planned items landed on branch `clips-493-default-log-level-call-sites` under PR #575; one (a chain-order Debug from `CLIAssemblyLoadingProvider`) was dropped mid-flight as unreachable through the level-gated pipeline. Change groups map to the decisions above:

- **Stream routing (D2/D3/D4/D7):** [`BuildStreams`](src/CLI/Program.cs), [`ConsoleLoggingService`](src/Core/Core.Runtime/ConsoleLoggingService.cs), and [`ConsoleOutputAdapter`](src/Core/Core.Runtime/ConsoleOutputAdapter.cs) all now write warn/info/verbose/debug to `Console.Error`; new [`ColorPolicy.ShouldEmitColor`](src/Core/Core.Runtime/ColorPolicy.cs) guards color at all eight sites (stderr uses `Console.IsErrorRedirected`, stdout uses `Console.IsOutputRedirected`); TUI [`ClassifyLine`](TUI/services/ProcessService.cs) recognizes the AzClips prefix set.
- **Default flip (D1):** [`CoreOptions.LoggingOptions.Level`](src/Core/Core.Abstractions/CoreOptions.cs), the [config schema mirror](src/Core/Core.Configuration/Schema/ConfigurationSchema.cs), and the two invalid-value fallbacks ([CLI](src/CLI/CliLoggingPolicy.cs), [PS](src/PS/Common/CmdletOutputStreamsFactory.cs)) all move `error` → `warn`. Tests updated in lockstep ([`ConfigCliTests`](tests/Cli.Tests/Configuration/ConfigCliTests.cs), [`CliLoggingPolicyTests`](tests/Cli.Tests/Configuration/CliLoggingPolicyTests.cs), [`CmdletOutputStreamsFactoryTests`](tests/Cmdlets.Tests/CmdletOutputStreamsFactoryTests.cs)).
- **Extension-load debug tracing (D5/D6):** new [`ExtensionLoadTracker`](src/CLI/AssemblyLoading/ExtensionLoadTracker.cs) accumulates per-load rows and emits an aggregated table on shutdown; both loader implementations in [`ExtensionLoader.cs`](src/CLI/AssemblyLoading/ExtensionLoader.cs) instrumented with entry/probe/hit/miss `[ClassName]`-prefixed Debug lines. Summary routes through `streams.WriteDebug` (level-gated) so the table stays silent under `warn`.
- **Tests (D9):** [`StreamRoutingIntegrationTests`](tests/Cli.Tests/StreamRoutingIntegrationTests.cs) (4 subprocess tests: `WriteError`→stderr; `warn` keeps stderr silent; `--debug` puts `DEBUG:` on stderr with stdout clean; `--debug` emits per-loader + `ExtensionLoadTracker` lines) and [`TuiClassifyLineTests`](tests/Cli.Tests/TuiClassifyLineTests.cs) (7 unit tests covering every prefix + fallthrough + ANSI stripping). `ClassifyLine` bumped to `internal static` with `InternalsVisibleTo="Cli.Tests"` for the unit tests.
- **Docs:** [`docs/azure-cli/docs-conceptual/config.md`](docs/azure-cli/docs-conceptual/config.md) gained a **Stream routing** subsection and refreshed sample outputs; [`eng-docs/spec/config-implementation.md`](eng-docs/spec/config-implementation.md) §10 "CLI Runtime Logging Policy" expanded to cover the default flip, stream contract, `LEVEL:` prefix, color gating, and the `EmitSummary` streams-vs-logger invariant.

Per-item commit SHAs and file lists live in the branch's git log — see [PR #575's file diff](https://github.com/Azure/azclips/pull/575/files) for the granular view.

---

## 4. Behavior audit under the new `warn` default

Codebase scan (2026-07-22) for `.Warn(...)` / `WriteWarning(...)` producers to enumerate what changes from "silent under `error`" to "visible under `warn`". Result: **two user-facing warning sites, both in `az login`.**

### Newly visible warnings

1. **`az login` — no subscriptions on the identity**
   [`LoginProcessor.cs:419`](src/Extensions/Login/Processors/LoginProcessor.cs#L419) — `logger.Warn("No subscriptions found for the logged-in identity. Continuing with tenant-level access; pass '--subscription' to target a subscription.")`. Fires when subscription auto-discovery completes but returns zero entries after a successful login. Actionable: the warning tells the user why no default subscription was picked and how to specify one.

2. **`az login` — subscription auto-discovery failure**
   [`LoginProcessor.cs:428`](src/Extensions/Login/Processors/LoginProcessor.cs#L428) — `logger.Warn($"Failed to auto-discover subscriptions: {ex.Message}. Continuing without a default subscription; pass '--subscription' to set one.")`. Fires when subscription auto-discovery throws (e.g. ARM transient failure, revoked permissions). Actionable: surfaces the underlying exception message and the workaround.

Both are on the intentional side of D1's PS-side note: under the new default the PS host applies `$WarningPreference = Continue` and shows them; callers who want the old silence use `-WarningAction Ignore`.

---

## 5. Validation

- `dotnet build azclips.slnx`
- `dotnet test tests/Cli.Tests/Cli.Tests.csproj`
- Manual: `az login --output json 2> /dev/null | jq .` and `az extension list 2> /dev/null | jq .` produce clean JSON.
- Manual (Windows): `Console.ForegroundColor` behavior on stderr, both piped and non-piped; `NO_COLOR=1 az foo` suppresses color.
- Manual: `AZCLIPS_LOG_LEVEL=debug az <cmd-that-loads-extension>` shows the new tracker summary and per-extension rows.
- Manual: `az --debug config get --key core.logging.level` — clean stdout, debug trace on stderr, still parseable when piped.
- Manual: `az config get --key core.logging.level` with no prior setting prints `core.logging.level = warn [default]` (previously `error`).
- Manual: an operation that emits `Warn` (e.g. login with no discoverable subscriptions — [`LoginProcessor.cs` L419](src/Extensions/Login/Processors/LoginProcessor.cs#L419)) surfaces the warning on stderr under the new default; previously silently swallowed.
- Manual (PS): import a module (e.g. `Import-Module src/PS/Login/Login.psd1`) and invoke a cmdlet that triggers `logger.Warn(...)` — confirm the warning surfaces in the PS host (default `$WarningPreference = Continue`). Then re-run with `-WarningAction Ignore` and confirm it's suppressed. Both should hold with no prior `core.logging.level` config set.
