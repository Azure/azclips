# Configuration Management

Azclips stores user-scope configuration in a global JSON file.

Configuration follows strict precedence: **command args > environment variables > global config > defaults**.

## Valid Configuration Keys

| Key                                           | Type   | Default            | Notes                                                                                                                                                                  |
| --------------------------------------------- | ------ | ------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `core.logging.level`                          | string | `error`            | Log level: `debug`, `info`, `warn`, `error`                                                                                                                            |
| `core.http.maxRetries`                        | int    | `3`                | Max HTTP request retries                                                                                                                                               |
| `core.http.maxRetriesFor429`                  | int    | `5`                | Max retries for 429 (rate limit) responses                                                                                                                             |
| `core.engine.extensionIndexCacheSize`         | int    | `512`              | LFU cache capacity for extension index (entry count)                                                                                                                   |
| `assemblyLoading.versionedInstallationDir`    | string | (system)           | Versioned assembly installation directory                                                                                                                              |
| `assemblyLoading.builtinInstallationDir`      | string | (system)           | Built-in assembly directory                                                                                                                                            |
| `assemblyLoading.extensionFolderPrefix`       | string | (see notes)        | Extension assembly folder prefix; `Microsoft.Azure.Azclips.Extensions.` (DEBUG), empty (Release)                                                                       |
| `telemetry.enabled`                           | bool   | `true`             | Enable telemetry                                                                                                                                                       |
| `telemetry.appInsightsInstrumentationKey`     | string | (empty)            | Application Insights key                                                                                                                                               |
| `telemetry.zipkinEndpoint`                    | string | (empty)            | Zipkin tracing endpoint                                                                                                                                                |
| `agent.enabled`                               | bool   | `false`            | Enable AI agent fallback (opt-in: `az config set --key agent.enabled --value true`)                                                                                    |
| `agent.provider`                              | string | `builtin`          | Agent model provider: `builtin` (default, Microsoft's AOAI deployment via APIM, requires `az login`) \| `github` (user's Copilot subscription, requires Copilot login) |
| `agent.sessionStyle`                          | string | `oneShot`          | Agent interaction mode: `oneShot` (single plan, then stop) or `interactive` (multi-turn)                                                                               |
| `agent.trace.enabled`                         | bool   | `false`            | Write a JSONL trace of each agent session (events, tool I/O) for local debugging/evaluation                                                                            |
| `agent.trace.path`                            | string | `~/.azclips/trace` | Directory for agent trace files (used when `agent.trace.enabled` is true; created on first write)                                                                      |
| `completion.enabled`                          | bool   | `true`             | Enable `az` shell tab-completion; set `false` to stop suggestions even while the completion script stays sourced                                                       |
| `extensions.resources.arm.apiVersionOverride` | string | (empty)            | Override ARM API version                                                                                                                                               |
| `clients.cli.output.format`                   | string | `table`            | CLI output format: `json`, `table`, `tsv`, `yaml`                                                                                                                      |
| `clients.powershell.output.format`            | string | `object`           | PowerShell output format: `object`, `json`, `table`                                                                                                                    |
| `defaults.group`                              | string | (empty)            | Default resource group                                                                                                                                                 |
| `defaults.location`                           | string | (empty)            | Default location                                                                                                                                                       |
| `defaults.web`                                | string | (empty)            | Default web app name                                                                                                                                                   |
| `defaults.vm`                                 | string | (empty)            | Default VM name                                                                                                                                                        |
| `defaults.vmss`                               | string | (empty)            | Default VMSS name                                                                                                                                                      |
| `defaults.acr`                                | string | (empty)            | Default ACR name                                                                                                                                                       |

## CLI Usage

`core.logging.level` controls runtime host logging verbosity in CLI sessions.
Use `--debug` to force debug-level output for a single invocation; this
override does not persist to config.

Use explicit parameter names for CLI config commands:

- `--key` for configuration key
- `--value` for configuration value

### Get configuration values or sections

```bash
# Retrieve a single key
az config get --key core.logging.level

# Output: core.logging.level = error [default]

# Retrieve a section (text output)
az config get --key core

# Example output:
#   logging.level = error
#   engine.extensionIndexCacheSize = 512
#   http.maxRetries = 3
#   http.maxRetriesFor429 = 5

# Retrieve a subsection as JSON
az config get --key core.logging --json

# Example output:
# {
#   "level": "error"
# }

# Retrieve a section as nested JSON
az config get --key core --json
```

### Set a configuration value

```bash
# Set a key
az config set --key core.logging.level --value debug

# Set with force (allows unknown keys)
az config set --key core.logging.level --value debug --force

# Output: Set core.logging.level = debug
```

### List all configuration

```bash
# List user config only
az config list

# List with defaults (shows all keys with their current values)
az config list --all

# Output as JSON
az config list --all --json
```

### Delete a configuration value

```bash
# Delete a key
az config delete --key core.logging.level

# Delete with force (allows unknown keys)
az config delete --key core.logging.level --force

# Output: Deleted core.logging.level
```

### View schema information

```bash
# Output schema metadata as JSON
az config info

# Output only key names (JSON array)
az config info --keys-only
```

## PowerShell Usage

### Import the Config module

```powershell
Import-Module ./src/PS/Config/Config.psd1
```

### Get configuration values or sections

```powershell
Get-AzConfig -Key core.logging.level
# Output: core.logging.level = error [default]

Get-AzConfig -Key core.logging.level -AsJson
# Output: {"Key":"core.logging.level","Value":"error","Source":"default"}

# Retrieve a section (text output)
Get-AzConfig -Key core

# Retrieve a subsection as JSON
Get-AzConfig -Key core.logging -AsJson
# Output: {"level":"error"}

# Retrieve a section as nested JSON
Get-AzConfig -Key core -AsJson
```

### Set a configuration value

```powershell
# Set a key (will prompt for confirmation)
Set-AzConfig -Key core.logging.level -Value debug

# Set with force (allows unknown keys)
Set-AzConfig -Key core.logging.level -Value debug -Force
```

### List all configuration

```powershell
# List user config only
Get-AzConfigList

# List with defaults
Get-AzConfigList -IncludeDefaults

# Output as JSON
Get-AzConfigList -IncludeDefaults -AsJson
```

### Delete a configuration value

```powershell
# Delete a key (will prompt for confirmation)
Remove-AzConfig -Key core.logging.level

# Delete with force (allows unknown keys)
Remove-AzConfig -Key core.logging.level -Force
```

### View schema information

```powershell
# List all valid configuration keys
Get-AzConfigInfo

# Output only key names
Get-AzConfigInfo -KeysOnly
```

## Environment Variable Mappings

You can override configuration using environment variables:

| Environment Variable                                                | Maps to                      |
| ------------------------------------------------------------------- | ---------------------------- |
| `AZCLIPS_LOG_LEVEL`                                                 | `core.logging.level`         |
| `PS_HTTP_MAX_RETRIES` / `AZURE_PS_HTTP_MAX_RETRIES`                 | `core.http.maxRetries`       |
| `PS_HTTP_MAX_RETRIES_FOR_429` / `AZURE_PS_HTTP_MAX_RETRIES_FOR_429` | `core.http.maxRetriesFor429` |
| `ZIPKIN_ENDPOINT`                                                   | `telemetry.zipkinEndpoint`   |

## Configuration File Location

Configuration is stored at:

- **Windows**: `%USERPROFILE%\.azclips\config.json`
- **Linux/macOS**: `$HOME/.azclips/config.json`

## Verification

### 1) Run defaults resolver test

```bash
dotnet test tests/Cli.Tests/Cli.Tests.csproj -c Release --filter "FullyQualifiedName~ConfigurationServiceTests.ResolveDefaultParameterAsync_ShouldReturnDefaultsValue_WhenConfigured"
```

Expected result:

- `defaults.*` values are resolved for parameter defaults when configured

### 2) Optional full config regression pass

```bash
dotnet test tests/Cli.Tests/Cli.Tests.csproj -c Release
```

This confirms defaults handling and command-surface regressions as a bundle.

## Examples

### Workflow: Enable debug logging

**CLI:**

```bash
az config set --key core.logging.level --value debug --force
az config get --key core.logging.level
```

**PowerShell:**

```powershell
Set-AzConfig -Key core.logging.level -Value debug -Force
Get-AzConfig -Key core.logging.level
```

### Workflow: Check all settings with defaults

**CLI:**

```bash
az config list --all
```

**PowerShell:**

```powershell
Get-AzConfigList -IncludeDefaults
```

### Workflow: Reset to defaults

**CLI:**

```bash
# Delete all user-set config
az config list | grep -v '\[default\]' | awk '{print $1}' | while read key; do
  az config delete --key "$key" --force
done
```

**PowerShell:**

```powershell
Get-AzConfigList | Where-Object { $_ -notmatch '\[default\]' } | ForEach-Object {
  $key = ($_ -split ' = ')[0]
  Remove-AzConfig -Key $key -Force
}
```
