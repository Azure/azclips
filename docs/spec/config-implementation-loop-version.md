Cirrus Configuration Service – Technical Solution
Status: Design Phase

🚀 Executive Summary
A unified, strongly-typed configuration service for Cirrus:

Centralizes config: All settings in ~/.cirrus/config.json
Typed access: Via IConfigurationService<T> with validation & schema awareness
Precedence hierarchy:
command arguments > environment variables > user config file > code defaults

Separation of concerns: Config is separate from credentials (AuthContextService/MSAL)
DI integration: Modular RegisterConfigurationServices() method
CLI & PowerShell support: Native commands/cmdlets & argument completers

🏗️ Architecture
Precedence order:

Command-line arguments
Environment variables
User config file (~/.cirrus/config.json)
Code defaults
Integration:
Dependency Injection via RegisterConfigurationServices().

📁 File Location & Format

Location: ~/.cirrus/config.json (user scope only)
Windows: %USERPROFILE%\.cirrus/config.json
macOS/Linux: $HOME/.cirrus/config.json
Format: Nested JSON, dot-notation key access

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
Hosts

hosts.cli.output.format – CLI output format (json, table, tsv, yaml; default: table)
hosts.powershell.output.format – PowerShell output format (object, json, table; default: object)

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
"hosts": {
"cli": { "output": { "format": "json" } },
"powershell": { "output": { "format": "object" } }
}
}

🛠️ Service Contract
IConfigurationService interface:

- GetConfiguration<T>() - Get typed config options, applying precedence
- SetConfigurationAsync(key, value) - Set value in user config file
- DeleteConfigurationAsync(key) - Delete key from user config file
- ListConfigurationAsync(includeDefaults) - List all keys in flattened format
- GetConfigFilePath() - Return path to user config file
- GetSchema() - Return schema metadata for tooling

💻 CLI Commands
Parent command: azv2 config
Subcommands:

get <key> – Retrieve value
set <key> <value> – Set value (confirmation, --force to skip)
delete <key> – Delete key
list [--all] – List all keys
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
Cmdlets:

Get-CirrusConfig -Key <key>
Set-CirrusConfig -Key <key> -Value <value> [-Force]
Remove-CirrusConfig -Key <key> [-Force]
Get-CirrusConfigList [-All]
Get-CirrusConfigInfo
Parameter Completion:

Auto-registered on module import
Tab completion for -Key parameter

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

IConfigurationService – Main interface
IConfigurationProvider – File I/O abstraction
IConfigurationValidator – Validation interface
ConfigurationService – Implementation
FileConfigurationProvider – JSON persistence
DefaultConfigurationValidator – Validation logic
Options: CoreOptions, TelemetryOptions, AgentOptions, ExtensionsOptions, HostsOptions
IConfigurationSchema – Schema metadata
Extensions.Config

ExtensionIndex.cs – Extension metadata
Commands: GetCommand, SetCommand, DeleteCommand, ListCommand, InfoCommand
ConfigKeyCompleter.cs – Argument completion
PS.Config (PowerShell Module)

Cmdlets: Get-CirrusConfig, Set-CirrusConfig, Remove-CirrusConfig, Get-CirrusConfigList, Get-CirrusConfigInfo
Parameter completion registration

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

🧪 Testing Strategy
Unit Tests

Schema validation, options parsing, file I/O, key normalization, precedence, error handling
Integration Tests

CLI commands, PowerShell cmdlets, env var override, config file creation, migration
Manual Testing

Manual config file creation, CLI commands, precedence, PowerShell cmdlets

📝 Notes
