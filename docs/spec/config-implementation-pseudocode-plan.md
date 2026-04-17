# Cirrus Config Service - Succinct Pseudocode Plan

**Status:** Complete and tested (as of 2026-04-16)
**Branch:** 11-config-implementation  
**Test Coverage:** 15 tests passing (CLI, PS, schema, service)

## Goal

Implement user-scope configuration with strict validation, safe writes, and CLI/PowerShell surfaces, using .NET configuration primitives internally.

## Scope (MVP)

- Single file: ~/.cirrus/config.json (global config)
- Profile-aware reads/writes using externally selected active profile context
- Precedence: command args > env vars > active profile config > global config > defaults
- Strict schema by default, allow unknown keys only with force
- Default parameters stored in config.defaults.\* (Azure CLI pattern)
- Agent connection settings remain env/.env (not config)
- Profile lifecycle (create/list/use/delete/select) is owned by a separate PR/spec

## High-Level Build Order

1. Core contracts and schema (include profile-aware resolution, defaults)
2. File provider and service implementation (active profile consumption, defaults resolution)
3. Wire into DI and migrate existing readers
4. Add CLI commands (config)
5. Add PowerShell cmdlets (config)
6. Add tests and docs

## Implementation Checklist (Current PR Revision)

### Core Contracts

- [ ] Add IProfileContextProvider abstraction (consumed, not owned)
- [ ] Extend IConfigurationService with ResolveDefaultParameter(name)
- [ ] Keep profile lifecycle APIs out of this PR

### Core Options + Schema

- [ ] Rename hosts._ schema/option model to clients._
- [ ] Add defaults.\* keys to schema (initial set: group, location, web, vm, vmss, acr)
- [ ] Add DefaultsOptions and ensure reflection-based defaults work for new keys

### Core Service Behavior

- [ ] Resolve active profile through IProfileContextProvider
- [ ] Layer config in order: defaults -> global config -> active profile -> env -> invocation
- [ ] Keep global config as mutation target for config set/delete in this PR
- [ ] Add ResolveDefaultParameter(name) implementation reading defaults.<name>

### DI Wiring

- [ ] Register IProfileContextProvider dependency (from profile service package/integration)
- [ ] Wire ConfigurationService with profile context dependency
- [ ] Ensure app starts when no active profile is available (null profile context)

### CLI + PowerShell Surface

- [ ] Keep command surface to 5 config commands only
- [ ] Update list/get source attribution to distinguish profile vs global when applicable
- [ ] Keep profile lifecycle command/cmdlet support out of this PR

### Tests

- [ ] Unit test: profile layering overrides global values
- [ ] Unit test: no active profile falls back to global + env + defaults
- [ ] Unit test: ResolveDefaultParameter(name) returns expected defaults.\* value
- [ ] Unit test: unknown defaults.\* keys rejected unless force
- [ ] Integration test: config command behavior unchanged for non-profile scenarios

### Validation + Docs Sync

- [ ] Run full Cli.Tests suite and confirm green
- [ ] Verify no regression in existing schema/service tests
- [ ] Update docs status/test counts to reflect actual post-implementation state

## Pseudocode

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

Create profile-awareness contracts:

- IProfileContextProvider – Returns currently selected profile context (name/path)
- IDefaultParameterResolver – Optional parameter injection for commands

#### Learnings

**What was built:**

- `src/Core/Core.Configuration/` — IConfigurationService, IConfigurationSchema, IConfigurationValidator
- `src/Core/Core.Configuration/Options/` — CoreOptions, TelemetryOptions, AssemblyLoadingOptions, etc.

**Key Pattern:** Options classes use reflection-based default extraction; schema keys are dotted paths matching property hierarchies (e.g., CoreOptions.Logging.Level → core.logging.level).

**Design Decision: Read-Only Access for Non-Config Services**

- Only the config extension/cmdlets have write access to the config file
- All other services and commands access config via IConfigurationService<T> with read-only semantics
- This prevents accidental mutations and ensures single responsibility (config service = config mutations, commands = read-only consumers)

**Profile Context Ownership:**

- Active profile selection is owned by a separate profile service/spec
- Config service consumes active profile name/path via IProfileContextProvider
- Config service does not provide profile lifecycle operations

### 2) Persistence + Core Service

Function ResolveConfigPath():

- if Windows => %USERPROFILE%/.cirrus/config.json
- else => $HOME/.cirrus/config.json

Function ResolveProfilePath(activeProfileName):

- base = ResolveConfigPath() directory
- if Windows => %USERPROFILE%/.cirrus/profiles/<activeProfileName>.json
- else => $HOME/.cirrus/profiles/<activeProfileName>.json

Function GetActiveProfileContext():

- active = profileContextProvider.GetActiveProfile()
- return active profile name/path or null

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
- merge global config file values (~/.cirrus/config.json)
- activeProfile = GetActiveProfileContext()
- if activeProfile exists, merge active profile values (overrides global)
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

#### Learnings

**What was built:**

- `src/Core/Core.Configuration/ConfigurationService.cs` — Set/Get/Delete/List with lock, atomic writes
- `src/Core/Core.DependencyInjection/` — Service registration

**Key Implementation Details:**

- Lock + atomic writes: Acquire lock → read file → modify in-memory JSON → write temp file → replace original → release lock. Prevents corruption on concurrent writes.
- Environment Mapping: CIRRUS_LOG_LEVEL → core.logging.level via explicit GetEnvironmentOverrides() mapping
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

#### Learnings

**What was built:**

- Service registration + DI wiring completed in bootstrapper
- Existing components (Engine, TelemetryService, Runtime) integrated with config service

### 4) CLI Surface

Add azv2 config group:

- get <key>
- set <key> <value> [--force]
- delete <key> [--force]
- list [--all] [--json]
- info (hidden)

Command behavior:

- set/delete prompt unless force
- clear error messages for unknown key/type/range failures
- include source in get/list output (e.g., [global], [profile:dev], [env], [default])
- tab completion from schema keys
- active profile is consumed from external profile context

#### Learnings

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
- ConfigPrecedence_ShouldLayerProfileOverGlobalConfig

### 5) PowerShell Surface

Add config cmdlets:

- Get-CirrusConfig
- Set-CirrusConfig
- Remove-CirrusConfig
- Get-CirrusConfigList
- Get-CirrusConfigInfo (hidden)

Cmdlet behavior:

- same validation and force semantics as CLI
- output typed PS objects
- register key completer on module import
- active profile is consumed from external profile context

#### Learnings

**What was built:**

- `src/PS/Config/` — Config PowerShell module (5 cmdlets)
- `src/Extensions/Config/ExtensionIndex.cs` — Extended with PS metadata (s_psTable)

**Key Implementation Details:**

1. **Separate Dispatch Tables:** PS commands registered by cmdlet name (Get-CirrusConfig), CLI by lowercase (get). ExtensionIndex has both s_psTable and s_cliTable; GetCommandMetadata(name, ClientType) dispatches correctly.
2. **Module Manifests:**
    - GUID must be valid hex only (0-9, a-f); invalid chars cause import failure
    - RequiredAssemblies path: `../../../artifacts/Debug/Core/` for Core DLLs and Microsoft.Extensions.\* dlls
3. **Parameter Mapping:** PS uses PascalCase (-AsJson, -IncludeDefaults), cmdlet function maps to CLI command names before calling engine
4. **ShouldProcess:** Set-CirrusConfig and Remove-CirrusConfig include confirmation dialog; skip with -Force flag
5. **Module Initialization:** Config.psm1 initializes DI container and loads all exported functions from exports/ folder

**Tests Passing:**

- PowerShellModule_ShouldImportAndExportFiveCmdlets
- PowerShellModule_ManifestShouldSpecifyAllRequiredDependencies

### 6) Tests (Minimum First)

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

#### Learnings

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
7. ConfigPrecedence_ShouldLayerProfileOverGlobalConfig
8. DefaultParameters_ShouldResolveFromConfig

**Test Patterns:**

- Fixture per test with temp directory and environment variable isolation (cache originals, clear on setup, restore on teardown)
- Path detection: Walk up from test assembly to find cirrus.slnx (survives different build configurations)
- Shared fixture for both CLI and PS (same underlying engine, different surfaces)
- All 15 config tests (3 CLI, 2 PS, 7 schema, 3 service) passing

### 7) Definition of Done (Completed)

- ✅ config service wired and used by migrated components
- ✅ CLI and PowerShell config commands functional
- ✅ config is profile-aware via external profile context provider
- ✅ default parameters supported (stored in config.defaults.\*)
- ✅ profile precedence layering verified (args > env > profile > global > defaults)
- ✅ strict validation enforced
- ✅ safe write semantics verified (locks, atomic writes)
- ✅ docs updated with user examples and workflows

#### Learnings

**Documentation Created:**

- `docs/howto/config.md` — User guide with valid keys table, CLI + PS examples, env var mappings, defaults patterns

**Additional Artifacts:**

- This implementation guide updated with all learnings, gotchas, and decision rationale

---

## Critical Technical Decisions (Summary)

1. **Namespace: hosts → clients** — Renamed to avoid confusion with server/machine context; aligns with ClientType (CLI vs PowerShell vs agent)
2. **Read-Only for Non-Config Services:** Only config extension has write access; all other components read via IConfigurationService<T>
3. **Profile Ownership Boundary:** Profile lifecycle is owned by a separate PR/spec; config only consumes active profile context
4. **Profile Precedence:** args > env > active profile > global config > defaults (profile values layer over global)
5. **Default Parameters (MVP):** Stored in config.defaults.\* (e.g., defaults.group, defaults.location); commands read via IConfigurationService.ResolveDefaultParameter() — opt-in, not automatic injection
6. **Bare Boolean Flags:** CLI uses --force, --all, --json without values; ArgumentParsingService treats empty array as true for bool type
7. **Separate Dispatch Tables:** PS and CLI each have their own command routing (cmdlet names vs lowercase command names)
8. **Default Reflection:** Defaults sourced from option classes via reflection, not flattened dicts, to preserve dotted schema keys
9. **Lock + Atomic Writes:** Cross-process lock + temp file swap prevents corruption on concurrent writes
10. **Environment Isolation in Tests:** Cache original env vars, clear on setup, restore on teardown to avoid ambient values shadowing test assertions

## Future Design Work (Phase 3+)

### Default Parameter Auto-Injection

- Implement DefaultInjectingService or parameter resolver middleware
- Commands auto-inject missing parameters from config.defaults.\* during initialization
- Requires command framework changes; deferred to separate architecture effort
- MVP: Commands read defaults explicitly; Phase 3: Framework-level auto-injection

## Gotchas & Lessons Learned

1. **GUID Format in Manifests:** Invalid hex characters cause module import failure; validate early
2. **Assembly Load Order:** Core DLLs before Extension DLLs in RequiredAssemblies
3. **Path Resolution:** Walk up from test assembly location rather than using relative paths
4. **PS vs CLI Parameter Names:** Map between -AsJson (PS) and json (CLI) in cmdlet function
5. **ShouldProcess Confirmation:** Set/Remove cmdlets prompt unless -Force is used
6. **Env Override Mapping:** CIRRUS_LOG_LEVEL must explicitly map to core.logging.level; not automatic

## Known Limitations / Future Work

- Config validation is structural only (known key, valid type); no range constraints (e.g., maxRetries > 0)
- Agent connection settings intentionally NOT in config; they stay in env/.env
- Tab completion for config keys not yet implemented
- PowerShell module publish (portable package layout) not yet done
- Default parameter auto-injection (DefaultInjectingService) deferred to Phase 3; MVP requires explicit opt-in from commands
- Profile lifecycle (create/list/use/delete/select) delivered in separate profile PR/spec
