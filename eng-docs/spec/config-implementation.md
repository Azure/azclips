# Design Spec: Configuration Service (Implemented MVP)

**Status:** Complete and tested (as of 2026-04-16)
**Branch:** 11-config-implementation  
**Test Coverage:** CLI, PS, schema, and service tests passing

## Overview

Implement user-scope configuration with strict validation, safe writes, and CLI/PowerShell surfaces, using .NET configuration primitives internally.

## Scope (MVP)

- Single file: ~/.azclips/config.json (global config)
- Precedence: command args > env vars > global config > defaults
- Strict schema by default, allow unknown keys only with force
- Default parameters stored under defaults.\* keys (Azure CLI pattern)
- Agent connection settings remain env/.env (not config)

## Implementation Phases (Historical)

1. Core contracts and schema (include defaults)
2. File provider and service implementation (defaults resolution)
3. Wire into DI and migrate existing readers
4. Add CLI commands (config)
5. Add PowerShell cmdlets (config)
6. Add tests and docs

## Implementation Checklist (Delivered)

### Core Contracts

- [x] Extend IConfigurationService with ResolveDefaultParameter(name)

### Core Options + Schema

- [x] Rename hosts._ schema/option model to clients._
- [x] Add defaults.\* keys to schema (initial set: group, location, web, vm, vmss, acr)
- [x] Add DefaultsOptions and ensure reflection-based defaults work for new keys

### Core Service Behavior

- [x] Layer config in order: defaults -> global config -> env -> invocation
- [x] Keep global config as mutation target for config set/delete in this PR
- [x] Add ResolveDefaultParameter(name) implementation reading defaults.<name>

### DI Wiring

- [x] Wire ConfigurationService without profile context dependencies

### CLI + PowerShell Surface

- [x] Keep command surface to 5 config commands only
- [x] Keep list/get source attribution for global/env/default

### Tests

- [x] Unit test: ResolveDefaultParameter(name) returns expected defaults.\* value
- [x] Unit test: unknown defaults.\* keys rejected unless force
- [x] Integration test: config command behavior unchanged for global config scenarios

### Validation + Docs Sync

- [x] Run full Cli.Tests suite and confirm green
- [x] Verify no regression in existing schema/service tests
- [x] Update docs status/test counts to reflect actual post-implementation state

## Design and Implementation Notes

### 1) Contracts + Schema

Define IConfigurationService:

- GetConfiguration<T>(optional key, optional default)
- SetConfigurationAsync(key, value, optional force)
- DeleteConfigurationAsync(key, optional force)
- ListConfigurationAsync(includeDefaults)
- GetConfigFilePath()
- GetSchema()

Define IConfigurationSchema:

- GetAllKeys()
- GetKeyMetadata(key)
- IsKnownKey(key)

Define IConfigurationValidator:

- ValidateKey(key, force)
- ValidateValue(key, value)
- ValidateOptions<T>(options)

Create options objects:

- CoreOptions
- AssemblyLoadingOptions
- TelemetryOptions
- AgentOptions (enabled only)
- ExtensionsOptions
- ClientsOptions
- DefaultsOptions (for command parameter defaults)

Create defaults contract:

- IDefaultParameterResolver – Optional parameter injection for commands

#### Implemented Notes

**What was built:**

- `src/Core/Core.Configuration/` — IConfigurationService, IConfigurationSchema, IConfigurationValidator
- `src/Core/Core.Configuration/Options/` — CoreOptions, TelemetryOptions, AssemblyLoadingOptions, etc.

**Key Pattern:** Options classes use reflection-based default extraction; schema keys are dotted paths matching property hierarchies (e.g., CoreOptions.Logging.Level → core.logging.level).

**Design Decision: Read-Only Access for Non-Config Services**

- Only the config extension/cmdlets have write access to the config file
- All other services and commands access config via IConfigurationService<T> with read-only semantics
- This prevents accidental mutations and ensures single responsibility (config service = config mutations, commands = read-only consumers)

**Ownership Boundary:**

- Config lifecycle in this branch is global config only
- No profile context dependency in config service

### 2) Persistence + Core Service

Function ResolveConfigPath():

- if Windows => %USERPROFILE%/.azclips/config.json
- else => $HOME/.azclips/config.json

Function SetConfigurationAsync(key, value, force):

- validator.ValidateKey(key, force)
- validator.ValidateValue(key, value)
- acquire exclusive cross-process lock
- json = read existing file or {}
- normalize key (colon -> dot)
- remove conflicting legacy flattened key if present
- set nested value by dot path
- atomic write temp file then replace
- release lock

Function DeleteConfigurationAsync(key, force):

- validator.ValidateKey(key, force)
- acquire lock
- json = read existing file
- delete nested value by dot path
- prune empty parent objects
- atomic write temp file then replace
- release lock

Function GetConfiguration<T>():

- result = defaults from schema/options
- merge global config file values (~/.azclips/config.json)
- merge environment variable overrides
- merge invocation overrides (from CLI/PS layer)
- bind to T and return

Function ResolveDefaultParameter(parameterName):

- read config key: defaults.<parameterName>
- return value or null if not set
- Note: Commands opt-in to reading this; not automatic injection in MVP

Function ListConfigurationAsync(includeDefaults):

- flatten nested json to dotted keys
- if includeDefaults add schema defaults for missing keys
- annotate each key with source

#### Implemented Notes

**What was built:**

- `src/Core/Core.Configuration/ConfigurationService.cs` — Set/Get/Delete/List with lock, atomic writes
- `src/Core/Core.DependencyInjection/` — Service registration

**Key Implementation Details:**

- Lock + atomic writes: Acquire lock → read file → modify in-memory JSON → write temp file → replace original → release lock. Prevents corruption on concurrent writes.
- Environment Mapping: AZCLIPS_LOG_LEVEL → core.logging.level via explicit GetEnvironmentOverrides() mapping
- Default precedence: Defaults come from reflection on option classes, not flattened service dict
- Source attribution tracked: [default], [env], [config file] in output

### 3) DI + Migrations

In RegisterConfigurationServices():

- add singleton schema
- add singleton validator
- add singleton file provider
- add singleton configuration service

In bootstrapper:

- call RegisterConfigurationServices in service registration chain

Migrate existing readers:

- AssemblyLoadingService reads AssemblyLoadingOptions
- Engine reads CoreOptions.Engine.ExtensionIndexCacheSize
- TelemetryService reads TelemetryOptions
- Runtime retry policy reads Core/Runtime options mapping

#### Implemented Notes

**What was built:**

- Service registration + DI wiring completed in bootstrapper
- Existing components (Engine, TelemetryService, Runtime) integrated with config service

### 4) CLI Surface

Add az config group:

- get <key>
- set <key> <value> [--force]
- delete <key> [--force]
- list [--all] [--json]
- info (hidden)

Command behavior:

- set/delete prompt via ShouldProcess/Confirm semantics
- clear error messages for unknown key/type/range failures
- include source in get/list output (e.g., [global], [env], [default])
- tab completion from schema keys

#### Implemented Notes

**What was built:**

- `src/Extensions/Config/Extensions.Config.csproj` — New extension project (5 commands)
- `src/Extensions/Config/ExtensionIndex.cs` — CLI command metadata (s_cliTable)
- `src/Extensions/Config/Commands/ConfigCommandBase.cs` — Shared base with GetResolvedEntriesAsync()
- `src/Extensions/Config/Commands/{Get,Set,Delete,List,Info}ConfigCommand.cs` — 5 config commands

**Key Changes From Plan:**

- **Bare boolean flags:** ArgumentParsingService treats empty flag value as true (e.g., --all parses as true) instead of requiring explicit value
- **Default sourcing:** Defaults extracted via reflection on option classes to preserve dotted schema keys
- **Dispatch routing:** CommandGroupIndex.cs maps "Config" group to Config extension; ExtensionDispatcher routes by extension + command name
- **Source tracking:** Output includes [default], [env], [config file] annotations

**Tests Passing:**

- ExtensionDispatcher_ShouldRouteConfigCommandGroup
- ArgumentParsingService_ShouldTreatBareBoolFlagsAsTrue
- ConfigCommands_ShouldSetGetListDeleteAndInfo
- ConfigPrecedence_ShouldLayerGlobalConfigWithDefaults

### 5) PowerShell Surface

Add config cmdlets:

- Get-AzConfig
- Set-AzConfig
- Remove-AzConfig
- Get-AzConfigList
- Get-AzConfigInfo

Cmdlet behavior:

- same validation and force semantics as CLI
- output typed PS objects
- register key completer on module import

#### Implemented Notes

**What was built:**

- `src/PS/Config/` — Config PowerShell module (5 cmdlets)
- `src/Extensions/Config/ExtensionIndex.cs` — Extended with PS metadata (s_psTable)

**Key Implementation Details:**

1. **Separate Dispatch Tables:** PS commands registered by cmdlet name (Get-AzConfig), CLI by lowercase (get). ExtensionIndex has both s_psTable and s_cliTable; GetCommandMetadata(name, ClientType) dispatches correctly.
2. **Module Manifests:**
    - GUID must be valid hex only (0-9, a-f); invalid chars cause import failure
    - RequiredAssemblies path: `../../../artifacts/Debug/Core/` for Core DLLs and Microsoft.Extensions.\* dlls
3. **Parameter Mapping:** PS uses PascalCase (-AsJson, -IncludeDefaults), cmdlet function maps to CLI command names before calling engine
4. **ShouldProcess:** Set-AzConfig and Remove-AzConfig use standard PowerShell ShouldProcess/Confirm semantics
5. **Module Initialization:** Config.psm1 initializes DI container and loads all exported functions from exports/ folder

**Tests Passing:**

- PowerShellModule_ShouldImportAndExportFiveCmdlets
- PowerShellModule_ManifestShouldSpecifyAllRequiredDependencies

### 6) Tests

Unit:

- key validation
- value validation (enum/range/uri/path)
- nested set/get/delete and prune
- lock + atomic write behavior
- precedence merge correctness

Integration:

- CLI set/get/delete/list happy path
- unknown key blocked unless force
- env overrides file
- PowerShell cmdlet parity with CLI

#### Implemented Notes

**What was built:**

- `tests/Cli.Tests/ConfigCliTests.cs` — Test file for CLI and PS config operations
- ConfigCommandFixture — Reusable harness for both surfaces (temp config, env isolation, mock handlers)

**Test Methods (Config):**

1. ExtensionDispatcher_ShouldRouteConfigCommandGroup
2. ArgumentParsingService_ShouldTreatBareBoolFlagsAsTrue
3. ConfigCommands_ShouldSetGetListDeleteAndInfo (end-to-end: set → get → list → delete → info)
4. PowerShellModule_ShouldImportAndExportConfigCmdlets
5. PowerShellModule_ManifestShouldSpecifyAllRequiredDependencies
6. ConfigPrecedence_ShouldLayerGlobalConfigWithDefaults
7. DefaultParameters_ShouldResolveFromConfig

**Test Patterns:**

- Fixture per test with temp directory and environment variable isolation (cache originals, clear on setup, restore on teardown)
- Path detection: Walk up from test assembly to find azclips.slnx (survives different build configurations)
- Shared fixture for both CLI and PS (same underlying engine, different surfaces)
- Config test suite passing for CLI, PS, schema, and service coverage

### 7) Definition of Done

- ✅ config service wired and used by migrated components
- ✅ CLI and PowerShell config commands functional
- ✅ default parameters supported (stored in config.defaults.\*)
- ✅ precedence layering verified (args > env > global > defaults)
- ✅ strict validation enforced
- ✅ safe write semantics verified (locks, atomic writes)
- ✅ docs updated with user examples and workflows

#### Implemented Notes

**Documentation Created:**

- `docs/azure-cli/docs-conceptual/config.md` — User guide with valid keys table, CLI + PS examples, env var mappings, defaults patterns

**Additional Artifacts:**

- This implementation guide updated with all learnings, gotchas, and decision rationale

---

## Critical Technical Decisions (Summary)

1. **Namespace: hosts → clients** — Renamed to avoid confusion with server/machine context; aligns with ClientType (CLI vs PowerShell vs agent)
2. **Read-Only for Non-Config Services:** Only config extension has write access; all other components read via IConfigurationService<T>
3. **Precedence:** args > env > global config > defaults
4. **Default Parameters (MVP):** Stored in defaults.\* keys (e.g., defaults.group, defaults.location); commands read via IConfigurationService.ResolveDefaultParameter() — opt-in, not automatic injection
5. **Bare Boolean Flags:** CLI uses --force, --all, --json without values; ArgumentParsingService treats empty array as true for bool type
6. **Separate Dispatch Tables:** PS and CLI each have their own command routing (cmdlet names vs lowercase command names)
7. **Default Reflection:** Defaults sourced from option classes via reflection, not flattened dicts, to preserve dotted schema keys
8. **Lock + Atomic Writes:** Cross-process lock + temp file swap prevents corruption on concurrent writes
9. **Environment Isolation in Tests:** Cache original env vars, clear on setup, restore on teardown to avoid ambient values shadowing test assertions
10. **CLI Runtime Logging Policy:** `core.logging.level` controls CLI host log stream emission; `--debug` is treated as a one-shot invocation override that takes precedence over env/global/default values.

    **Default:** `warn` (changed from `error` in PR #575 / issue #493). Chosen for Azure-family peer symmetry: Azure CLI Python's console default is `Warning` ([azlogging.py](https://github.com/Azure/azure-cli/blob/dev/src/azure-cli-core/azure/cli/core/azlogging.py)) and Az PowerShell's effective baseline is `Warning + Error` ([about_preference_variables](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_preference_variables?view=powershell-7.6)). AWS CLI is `Error`-only but has no `warn` tier at all \u2014 different data model, not a competing default. `info`-as-default was rejected: no cited peer defaults console to Info, and it would require call-site retriage of every existing `Info(...)` call. The invalid/empty-value fallback in [`CliLoggingPolicy`](../../src/CLI/CliLoggingPolicy.cs) and [`CmdletOutputStreamsFactory`](../../src/PS/Common/CmdletOutputStreamsFactory.cs) was moved from `Error` to `Warn` in lockstep so a malformed config value degrades to the new default rather than the old one.

    **PS-side effect (intentional):** the config is shared, so PS cmdlets read `warn` too. Under the old `error` default, [`CmdletOutputStreamsFactory.WriteWarning`](../../src/PS/Common/CmdletOutputStreamsFactory.cs) silenced warnings before they reached the PS host; under `warn` they flow through `cmdlet.WriteWarning(...)` and the host applies its own `$WarningPreference` (default `Continue` = shown). Info/Verbose/Debug behavior is unchanged. Callers who want the old silence use `-WarningAction Ignore`.

    **Stream routing contract (POSIX/GNU):** Adopted by every peer CLI examined (Azure CLI Python, Az PowerShell, AWS CLI, `git`, `kubectl`, `docker`, `terraform`) and codified in [GNU Coding Standards \u00a74.4](https://www.gnu.org/prep/standards/html_node/Errors.html) (_"All error messages should be directed to stderr rather than stdout."_):
    - **stdout** carries **command results only** (JSON payloads, table rows, resource IDs). `WriteOutput` writes to `Console.Out`.
    - **stderr** carries **every diagnostic level**: `error`, `warn`, `info`, `verbose`, `debug`. `WriteError` / `WriteWarning` / `WriteInformation` / `WriteVerbose` / `WriteDebug` all write to `Console.Error` (see [`BuildStreams` in `Program.cs`](../../src/CLI/Program.cs)). Prior to PR #575 the last four routed to stdout, polluting `--output json` pipelines \u2014 a bug, not a philosophy.

    **Prefix format:** `LEVEL: message` \u2014 uppercase word, single colon, single space. Applied uniformly across `BuildStreams`, [`ConsoleLoggingService`](../../src/Core/Core.Runtime/ConsoleLoggingService.cs), and [`AgentEventStyle`](../../Agent/Hosting/AgentEventStyle.cs). Matches Azure CLI Python's `WARNING:` output and the Python `logging` default formatter (same peer-symmetry rationale as the default flip); matches the PS host's own default rendering of `cmdlet.WriteWarning("x")`; follows the human-facing CLI convention `program: level: message` rather than the log-aggregator convention `[LEVEL]`. Full word `WARNING`/`VERBOSE`/`DEBUG` (not `WARN`) matches Python level names. Consolidated in the same PR because AzClips is pre-production and no downstream parser has locked in a style yet \u2014 later would be a breaking change.

    **Color gating:** [`ColorPolicy.ShouldEmitColor(bool targetIsRedirected)`](../../src/Core/Core.Runtime/ColorPolicy.cs) applies the precedence chain `NO_COLOR` env → `FORCE_COLOR` env → `core.output.color = always` → `core.output.color = never` → `TERM=dumb` → redirection check → else color, at every color site. Env vars sit above config because env is a per-invocation override and config is persistent policy; matches git / cargo / npm convention. Because diagnostics went from stdout to stderr, the stderr sites pass `Console.IsErrorRedirected` (not `Console.IsOutputRedirected`) so ANSI escapes are suppressed when stderr is piped/captured.

    **Persistent color preference (`core.output.color`):** three-state string enum (`auto` | `always` | `never`), default `auto`. `Program.Main` calls `ColorPolicy.Initialize(ColorPolicy.Parse(...))` once after DI init, before `BuildStreams`. `_mode` is a static field cached on `ColorPolicy`; there is no per-emission config lookup. Invalid values fall back to `auto` (the schema's `AllowedValues` blocks invalid writes at `az config set` time, so this fallback is only for hand-edited or corrupted config files). `auto` preserves the pre-config behavior byte-for-byte, so users who never touch the setting see no change. CLI-scoped: PowerShell diagnostics render through `cmdlet.WriteWarning`/`WriteError`/etc. which the PS host colors independently of this config.

    **Extension-load debug tracing:** [`ExtensionLoadTracker`](../../src/CLI/AssemblyLoading/ExtensionLoadTracker.cs) accumulates per-extension load rows during a single invocation and emits an aggregated timing table in `Program.Main`'s `finally` block. All output is at `Debug` level. The tracker is routed through `streams.WriteDebug` (level-gated by `BuildStreams`) rather than through `ILoggingService` \u2014 the `finally` runs after the session's `UseScope` is disposed, so a plain `ILoggingService.Debug(...)` call would fall back to the ungated `ConsoleLoggingService` and leak the table under `warn`/`error`.

## Future Design Work (Phase 3+)

### Default Parameter Auto-Injection

- Implement DefaultInjectingService or parameter resolver middleware
- Commands auto-inject missing parameters from defaults.\* keys during initialization
- Requires command framework changes; deferred to separate architecture effort
- MVP: Commands read defaults explicitly; Phase 3: Framework-level auto-injection

## Gotchas & Lessons Learned

1. **GUID Format in Manifests:** Invalid hex characters cause module import failure; validate early
2. **Assembly Load Order:** Core DLLs before Extension DLLs in RequiredAssemblies
3. **Path Resolution:** Walk up from test assembly location rather than using relative paths
4. **PS vs CLI Parameter Names:** Map between -AsJson (PS) and json (CLI) in cmdlet function
5. **ShouldProcess Confirmation:** Set/Remove cmdlets rely on standard PowerShell WhatIf/Confirm behavior
6. **Env Override Mapping:** AZCLIPS_LOG_LEVEL must explicitly map to core.logging.level; not automatic

## Known Limitations / Future Work

- Config validation enforces known keys, valid types, allowed values, and numeric min/max bounds where defined by the schema
- Agent connection settings intentionally NOT in config; they stay in env/.env
- Tab completion for config keys not yet implemented
- PowerShell module publish (portable package layout) not yet done
- Default parameter auto-injection (DefaultInjectingService) deferred to Phase 3; MVP requires explicit opt-in from commands
