# Cirrus Config Service - Succinct Pseudocode Plan

Status: Pre-implementation plan
Date: 2026-04-15

## Goal

Implement user-scope configuration with strict validation, safe writes, and CLI/PowerShell surfaces, using .NET configuration primitives internally.

## Scope (MVP)

- Single file: ~/.cirrus/config.json
- Precedence: command args > env vars > config file > defaults
- Strict schema by default, allow unknown keys only with force
- Agent connection settings remain env/.env (not config)

## High-Level Build Order

1. Core contracts and schema
2. File provider and service implementation
3. Wire into DI and migrate existing readers
4. Add CLI commands
5. Add PowerShell cmdlets
6. Add tests and docs

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
- HostsOptions

### 2) Persistence + Core Service

Function ResolveConfigPath():

- if Windows => %USERPROFILE%/.cirrus/config.json
- else => $HOME/.cirrus/config.json

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
- merge user file values
- merge environment values
- merge invocation overrides (from CLI/PS layer)
- bind to T and return

Function ListConfigurationAsync(includeDefaults):

- flatten nested json to dotted keys
- if includeDefaults add schema defaults for missing keys
- annotate each key with source

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
- include source in get/list output
- tab completion from schema keys

### 5) PowerShell Surface

Add cmdlets:

- Get-CirrusConfig
- Set-CirrusConfig
- Remove-CirrusConfig
- Get-CirrusConfigList
- Get-CirrusConfigInfo (hidden)

Cmdlet behavior:

- same validation and force semantics as CLI
- output typed PS objects
- register key completer on module import

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

### 7) Definition of Done

- config service wired and used by migrated components
- CLI and PowerShell commands functional
- strict validation enforced
- safe write semantics verified
- docs updated with user examples
