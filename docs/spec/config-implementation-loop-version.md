Cirrus Configuration Service – Technical Solution
Status: Design Phase

🚀 Executive Summary
A unified, strongly-typed configuration service for Cirrus:

Centralizes config: All settings in ~/.cirrus/config.json with profile-aware resolution
Typed access: Via IConfigurationService<T> with validation & schema awareness
Config profiles: Active profile consumed from external profile service/spec
Default parameters: Azure CLI–style defaults (defaults.group, defaults.location, etc.)
Precedence hierarchy:
command arguments > environment variables > active profile > global config file > code defaults

Separation of concerns: Config is separate from credentials (AuthContextService/MSAL)
DI integration: Modular RegisterConfigurationServices() method
CLI & PowerShell support: Native config commands/cmdlets

🏗️ Architecture
Precedence order:

Command-line arguments
Environment variables
Active profile config file (~/.cirrus/profiles/<name>.json)
Global config file (~/.cirrus/config.json)
Code defaults
Integration:
Dependency Injection via RegisterConfigurationServices().

📁 File Location & Format

Location: ~/.cirrus/config.json (global config)
Profiles: ~/.cirrus/profiles/<name>.json (read when active profile is provided by external profile context)
Windows: %USERPROFILE%\.cirrus/config.json and %USERPROFILE%\.cirrus/profiles/
macOS/Linux: $HOME/.cirrus/config.json and $HOME/.cirrus/profiles/
Format: Nested JSON, dot-notation key access
Note: active profile selection state is owned by a separate profile service/spec

🗂️ Key Catalog
Core Settings

core.logging.level – Log level (debug, info, warn, error)
core.engine.extensionIndexCacheSize – LFU cache size (default: 512)
Assembly Loading

assemblyLoading.builtinInstallationDir – Built-in extensions directory
assemblyLoading.versionedInstallationDir – User extensions directory (default: ~/.cirrus/extensions)
assemblyLoading.extensionFolderPrefix – Extension discovery prefix (default: Microsoft.Azure.Cirrus.Extensions.)
Telemetry

telemetry.enabled – Enable/disable telemetry (default: true)
telemetry.zipkinEndpoint – Zipkin endpoint
telemetry.appInsightsInstrumentationKey – App Insights (future)
Agent

agent.enabled – Enable/disable agent features (default: true)
Extensions

extensions.resources.arm.apiVersionOverride – ARM API version override
Clients

clients.cli.output.format – CLI output format (json, table, tsv, yaml; default: table)
clients.powershell.output.format – PowerShell output format (object, json, table; default: object)

Defaults

defaults.group – Default resource group for all commands
defaults.location – Default location for all commands
defaults.web – Default app name for az webapp commands (extensible per-command pattern)

📝 Sample Config File

{
"core": {
"logging": { "level": "debug" },
"engine": { "extensionIndexCacheSize": 768 }
},
"assemblyLoading": {
"builtinInstallationDir": "/path/to/extensions",
"versionedInstallationDir": "/home/user/.cirrus/extensions",
"extensionFolderPrefix": "Microsoft.Azure.Cirrus.Extensions."
},
"telemetry": {
"enabled": true,
"zipkinEndpoint": "http://localhost:9411/api/v2/spans"
},
"agent": { "enabled": true },
"extensions": {
"resources": {
"arm": { "apiVersionOverride": null }
}
},
"clients": {
"cli": { "output": { "format": "json" } },
"powershell": { "output": { "format": "object" } }
},
"defaults": {
"group": "my-production-rg",
"location": "eastus"
}
}

🛠️ Service Contract
IConfigurationService interface:

- GetConfiguration<T>() - Get typed config options, applying precedence (global + active profile + env + args)
- SetConfigurationAsync(key, value) - Set value in global config file
- DeleteConfigurationAsync(key) - Delete key from global config file
- ListConfigurationAsync(includeDefaults) - List all keys in flattened format (with source attribution)
- GetConfigFilePath() - Return path to global config file
- GetSchema() - Return schema metadata for tooling
- ResolveDefaultParameter(name) - Read parameter default from config.defaults.\* (opt-in for commands)

IProfileContextProvider interface (consumed from separate profile service/spec):

- GetActiveProfile() - Return active profile context (name/path) or null

💻 CLI Commands
Parent command: azv2 config
Subcommands:

get <key> – Retrieve value with source attribution
set <key> <value> – Set value in global config (confirmation, --force to skip)
delete <key> – Delete key from global config
list [--all] – List all keys in global config
info – Schema metadata (hidden)

Validation:

Unknown keys rejected by default
--force allows experimental keys
Type, enum, range, path, URI validation

Argument Completion:

Tab completion for config keys
Example: azv2 config set core.<TAB>

🖥️ PowerShell Cmdlets
Module: Cirrus.Config

Config Cmdlets:

- Get-CirrusConfig -Key <key>
- Set-CirrusConfig -Key <key> -Value <value> [-Force]
- Remove-CirrusConfig -Key <key> [-Force]
- Get-CirrusConfigList [-All]
- Get-CirrusConfigInfo

Parameter Completion:

Auto-registered on module import
Tab completion for -Key parameter (config cmdlets)

🗃️ File Operations
Write (Set Operation)

1. Acquire cross-process exclusive lock
2. Read config.json (or create empty `{}`)
3. Normalize key (convert colons to dots)
4. Navigate to parent path
5. Remove conflicting flattened legacy keys
6. Set new value
7. Write to temp file (atomic swap)
8. Replace config.json
9. Release lock

Delete Operation

1. Acquire cross-process exclusive lock
2. Read config.json
3. Delete key
4. Prune empty parent objects
5. Write with atomic swap
6. Release lock

Key Features:

Atomic writes (temp file + replace)
Cross-process locking
Empty parent cleanup
Legacy key conflict resolution

✅ Validation Rules

Key must exist in schema (strict by default)
Unknown keys rejected (--force allows)
Value type must match (string, int, bool, etc.)
Enum values in allowed list
Integer values in specified range
Paths must exist or be creatable
URIs must be valid absolute URIs
Regex patterns must match if specified

📦 New Modules
Core.Configuration

IConfigurationService – Main interface (now includes ResolveDefaultParameter)
IProfileContextProvider – Active profile context provider (consumed from separate profile service/spec)
IConfigurationProvider – File I/O abstraction
IConfigurationValidator – Validation interface
ConfigurationService – Implementation (includes profile layering)
FileConfigurationProvider – JSON persistence (handles global + profile files)
DefaultConfigurationValidator – Validation logic
Options: CoreOptions, TelemetryOptions, AgentOptions, ExtensionsOptions, ClientsOptions, DefaultsOptions
IConfigurationSchema – Schema metadata

Extensions.Config

ExtensionIndex.cs – Extension metadata (5 commands)
Commands: GetCommand, SetCommand, DeleteCommand, ListCommand, InfoCommand (config)
ConfigKeyCompleter.cs – Argument completion for config keys

PS.Config (PowerShell Module)

Config Cmdlets: Get-CirrusConfig, Set-CirrusConfig, Remove-CirrusConfig, Get-CirrusConfigList, Get-CirrusConfigInfo
Parameter completion registration (keys)

🔄 Changes to Existing Code
Minimal changes to core services:

CirrusServiceProvider.cs – Add .RegisterConfigurationServices()
AssemblyLoadingService.cs – Inject IConfigurationService<AssemblyLoadingOptions>
Engine.cs – Inject IConfigurationService<CoreOptions>
TelemetryService.cs – Inject IConfigurationService<TelemetryOptions>

🛤️ Migration Path
Phase 1: Abstractions & DI

Define contracts, options, schema interfaces
Register in DI container
Phase 2: Service & Migration

Implement ConfigurationService, FileConfigurationProvider, validation
Migrate hardcoded values
Phase 3: CLI Commands

Implement config extension, commands, argument completion
Phase 4: PowerShell

Create PowerShell module, cmdlets, parameter completion

🔗 Environment Variable Mapping
For backward compatibility:

ZIPKIN_ENDPOINT → telemetry.zipkinEndpoint
PS_HTTP_MAX_RETRIES_FOR_429 → core.http.maxRetriesFor429
CIRRUS_LOG_LEVEL → core.logging.level
AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_DEPLOYMENT, AZURE_OPENAI_API_KEY → remain in env/.env (MVP)

🤖 AI Configuration Strategy
MVP: Keep Azure OpenAI settings in env/.env

Connection details = service wiring, not user preferences
Secrets not in config files
Only agent.enabled in config for behavior toggles

🧠 Design Decisions
Single User Scope

No local/global split, no per-project variants (MVP)
Single file at ~/.cirrus/config.json
User settings apply globally (to user)
Nested JSON

Readable, natural grouping, .NET conventions
Typed Options Objects

Strongly-typed, compile-time safety, IDE autocomplete
Validation at Write

Catch errors early, prevent invalid JSON, immediate feedback

📝 Note: Alignment with .NET IConfiguration
Cirrus should be based on Microsoft.Extensions.Configuration.IConfiguration internally.
✅ What .NET IConfiguration Provides Out of the Box

Hierarchical key/value configuration model
Provider composition and precedence layering
Typed binding patterns for options objects
Optional change tokens for reload scenarios
🛠 What Cirrus Still Needs in Its Own Layer

Strict schema validation
Unknown keys are rejected by default (unless forced)

Writable config operations
Support for set, delete, list with cross-process locking and atomic writes

Schema metadata
For CLI/PowerShell completions and docs tooling

Legacy key normalization and conflict cleanup rules
💡 Design Decision

Keep IConfigurationService as the Cirrus-facing contract
Implement it on top of .NET’s IConfiguration / IConfigurationRoot under the hood

// Cirrus-facing contract
public interface IConfigurationService
{
// ...methods as previously defined
}

// Implementation wraps .NET's IConfiguration
public class ConfigurationService : IConfigurationService
{
private readonly IConfigurationRoot \_configRoot;
// ...
}

🔐 Access Control

**Write Access:**

- Only config extension/cmdlets (azv2 config set/delete) have write access
- Non-config commands and services have read-only access via IConfigurationService<T>
- This prevents accidental mutations and ensures config integrity

**Read Access:**

- All services and commands read config via IConfigurationService<T>.GetConfiguration<T>()
- No direct file access; all reads go through the service with full precedence chain (args > env > profile > global > defaults)

📋 Config Profile Integration (MVP)

**Design:** Resolve config against the currently active profile context

**Ownership:**

- Profile lifecycle and selection are owned by a separate profile PR/spec
- Config service consumes active profile context via IProfileContextProvider
- Each profile file is layered over global config when an active profile is present

**Precedence (with profiles):**

1. Command-line arguments
2. Environment variables
3. Active profile config file (~/.cirrus/profiles/<active>.json)
4. Global config file (~/.cirrus/config.json)
5. Code defaults

**Out of Scope in this PR:**

- Profile lifecycle commands/cmdlets (create/list/use/delete/select)
- Profile storage ownership and active profile state persistence

📌 Default Parameters (MVP)

**Design:** Support preconfigured defaults for command parameters, following Azure CLI pattern

**Storage:**

- Stored in config under `defaults.*` namespace
- Examples: `defaults.group`, `defaults.location`, `defaults.vm`, `defaults.acr` (extensible)
- User can set: `azv2 config set defaults.group my-default-rg`
- Values apply globally to all users of the same account

**Parameter Resolution:**

- Commands can call `IConfigurationService.ResolveDefaultParameter(name)` to check for defaults
- Returns value from config.defaults.\* if set, null otherwise
- Commands opt-in to reading defaults; not framework-enforced in MVP
- Framework-level auto-injection (DefaultInjectingService) deferred to Phase 3

**Example Workflow:**

```
$ azv2 config set defaults.group my-rg
$ azv2 group show  # command can read defaults.group if implemented
```

🧪 Testing Strategy
Unit Tests

- Schema validation, options parsing, file I/O, key normalization
- Precedence chain (args > env > profile > global > defaults)
- Profile context consumption and layering behavior
- Default parameter resolution
- Error handling (missing profile, invalid names)
- Lock and atomic write behavior

Integration Tests

- CLI config commands (get, set, delete, list, info)
- PowerShell cmdlets (config)
- Environment variable overrides
- Profile layering (active profile values override global)
- Default parameters accessible via ResolveDefaultParameter
- Config file creation and migration

Manual Testing

- Config file creation and editing
- Active profile integration (with profile selected externally)
- Default parameter setting and retrieval
- Precedence validation across all layers
- PowerShell module import and cmdlet invocation

📝 Notes
