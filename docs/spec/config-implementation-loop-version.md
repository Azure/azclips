Cirrus Configuration Service – Technical Solution
Status: Design Phase

🚀 Executive Summary
A unified, strongly-typed configuration service for Cirrus:

Centralizes config: All settings in ~/.cirrus/config.json with optional named profiles
Typed access: Via IConfigurationService<T> with validation & schema awareness
Config profiles: Named sets (dev, prod, staging) that layer over global config
Default parameters: Azure CLI–style defaults (defaults.group, defaults.location, etc.)
Precedence hierarchy:
command arguments > environment variables > active profile > global config file > code defaults

Separation of concerns: Config is separate from credentials (AuthContextService/MSAL)
DI integration: Modular RegisterConfigurationServices() method
CLI & PowerShell support: Native commands/cmdlets for config + profiles with completers

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

Location: ~/.cirrus/config.json (global config, includes active profile state)
Profiles: ~/.cirrus/profiles/<name>.json (named configuration sets)
Windows: %USERPROFILE%\.cirrus/config.json and %USERPROFILE%\.cirrus/profiles/
macOS/Linux: $HOME/.cirrus/config.json and $HOME/.cirrus/profiles/
Format: Nested JSON, dot-notation key access
Reserved Key: profile.active (stores active profile name in global config)

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
"profile": { "active": "dev" },
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

IConfigurationProfileService interface (new):

- GetActiveProfile() - Return name of currently active profile or null
- SetActiveProfileAsync(name) - Switch to named profile (validate existence)
- CreateProfileAsync(name) - Create new profile file
- DeleteProfileAsync(name) - Delete profile (prevent if active)
- ListProfilesAsync() - List all profile names
- GetProfileAsync(name) - Read profile contents

💻 CLI Commands
Parent command: azv2 config
Subcommands:

get <key> – Retrieve value with source attribution
set <key> <value> – Set value in global config (confirmation, --force to skip)
delete <key> – Delete key from global config
list [--all] – List all keys in global config
info – Schema metadata (hidden)

Parent command: azv2 config profile
Subcommands:

use <name> – Set active profile (or create if missing with --create flag)
list – Show all profiles, mark active profile with (active) indicator
create <name> – Create new empty profile file
delete <name> – Delete profile (fail if active)
show <name> – Display profile contents (like config list but for specific profile)

Validation:

Unknown keys rejected by default
--force allows experimental keys
Type, enum, range, path, URI validation
Profile names must be alphanumeric + hyphens

Argument Completion:

Tab completion for config keys
Tab completion for profile names
Example: azv2 config set core.<TAB> or azv2 config profile use <TAB>

🖥️ PowerShell Cmdlets
Module: Cirrus.Config

Config Cmdlets:

- Get-CirrusConfig -Key <key>
- Set-CirrusConfig -Key <key> -Value <value> [-Force]
- Remove-CirrusConfig -Key <key> [-Force]
- Get-CirrusConfigList [-All]
- Get-CirrusConfigInfo

Profile Cmdlets:

- Use-CirrusConfigProfile -Name <name> [-Create]
- Get-CirrusConfigProfile – List all profiles with active marker
- New-CirrusConfigProfile -Name <name>
- Remove-CirrusConfigProfile -Name <name> [-Force]
- Show-CirrusConfigProfile -Name <name>

Parameter Completion:

Auto-registered on module import
Tab completion for -Key parameter (config cmdlets)
Tab completion for -Name parameter (profile cmdlets)

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
IConfigurationProfileService – Profile management interface (new)
IConfigurationProvider – File I/O abstraction
IConfigurationValidator – Validation interface
ConfigurationService – Implementation (includes profile layering)
FileConfigurationProvider – JSON persistence (handles global + profile files)
DefaultConfigurationValidator – Validation logic
Options: CoreOptions, TelemetryOptions, AgentOptions, ExtensionsOptions, ClientsOptions, DefaultsOptions, ProfileStateOptions
IConfigurationSchema – Schema metadata

Extensions.Config

ExtensionIndex.cs – Extension metadata (10 commands: 5 config + 5 profile)
Commands: GetCommand, SetCommand, DeleteCommand, ListCommand, InfoCommand (config)
Commands: UseProfileCommand, ListProfileCommand, CreateProfileCommand, DeleteProfileCommand, ShowProfileCommand (profile)
ConfigKeyCompleter.cs – Argument completion for config keys
ProfileNameCompleter.cs – Argument completion for profile names

PS.Config (PowerShell Module)

Config Cmdlets: Get-CirrusConfig, Set-CirrusConfig, Remove-CirrusConfig, Get-CirrusConfigList, Get-CirrusConfigInfo
Profile Cmdlets: Use-CirrusConfigProfile, Get-CirrusConfigProfile, New-CirrusConfigProfile, Remove-CirrusConfigProfile, Show-CirrusConfigProfile
Parameter completion registration (keys + profile names)

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

- Only config extension/cmdlets (azv2 config set/delete, azv2 config profile create/delete) have write access
- Non-config commands and services have read-only access via IConfigurationService<T>
- This prevents accidental mutations and ensures config integrity

**Read Access:**

- All services and commands read config via IConfigurationService<T>.GetConfiguration<T>()
- No direct file access; all reads go through the service with full precedence chain (args > env > profile > global > defaults)

📋 Config Profiles (MVP)

**Design:** Support named configuration profiles (dev, prod, staging, custom)

**Implementation:**

- User selects active profile: `azv2 config profile use dev`
- Each profile has its own .json file: `~/.cirrus/profiles/dev.json`, `~/.cirrus/profiles/prod.json`
- Active profile name stored in global config at reserved key: `profile.active`
- Profile values layer on top of global config in precedence chain
- Service automatically loads and applies active profile during GetConfiguration<T>()

**Precedence (with profiles):**

1. Command-line arguments
2. Environment variables
3. Active profile config file (~/.cirrus/profiles/<active>.json)
4. Global config file (~/.cirrus/config.json)
5. Code defaults

**Commands (Implemented):**

- `azv2 config profile use <name>` – Set active profile (validate profile exists)
- `azv2 config profile list` – Show all profiles with (active) marker
- `azv2 config profile create <name>` – Create new empty profile
- `azv2 config profile delete <name>` – Delete profile (prevent if active)
- `azv2 config profile show <name>` – Display profile contents

**PowerShell Equivalents:**

- Use-CirrusConfigProfile
- Get-CirrusConfigProfile
- New-CirrusConfigProfile
- Remove-CirrusConfigProfile
- Show-CirrusConfigProfile

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
- Profile selection and state persistence
- Default parameter resolution
- Error handling (missing profile, invalid names)
- Lock and atomic write behavior

Integration Tests

- CLI config commands (get, set, delete, list, info)
- CLI profile commands (use, list, create, delete, show)
- PowerShell cmdlets (config and profile)
- Environment variable overrides
- Profile layering (active profile values override global)
- Default parameters accessible via ResolveDefaultParameter
- Config file creation and migration

Manual Testing

- Config file creation and editing
- Profile workflows (create, switch, delete)
- Default parameter setting and retrieval
- Precedence validation across all layers
- PowerShell module import and cmdlet invocation

📝 Notes
