# Configuration Management

Cirrus stores user-scope configuration in a global JSON file and can optionally layer an active profile configuration file on top.

Configuration follows strict precedence: **command args > environment variables > active profile config > global config > defaults**.

## Valid Configuration Keys

| Key                                           | Type   | Default              | Notes                                               |
| --------------------------------------------- | ------ | -------------------- | --------------------------------------------------- |
| `core.logging.level`                          | string | `info`               | Log level: `debug`, `info`, `warning`, `error`      |
| `core.http.maxRetries`                        | int    | `3`                  | Max HTTP request retries                            |
| `core.http.maxRetriesFor429`                  | int    | `5`                  | Max retries for 429 (rate limit) responses          |
| `core.engine.extensionIndexCacheSize`         | int    | `512`                | Extension index cache size in MB                    |
| `assemblyLoading.versionedInstallationDir`    | string | (system)             | Versioned assembly installation directory           |
| `assemblyLoading.builtinInstallationDir`      | string | (system)             | Built-in assembly directory                         |
| `assemblyLoading.extensionFolderPrefix`       | string | `.cirrus/extensions` | Extensions folder prefix                            |
| `telemetry.enabled`                           | bool   | `true`               | Enable telemetry                                    |
| `telemetry.appInsightsInstrumentationKey`     | string | (empty)              | Application Insights key                            |
| `telemetry.zipkinEndpoint`                    | string | (empty)              | Zipkin tracing endpoint                             |
| `agent.enabled`                               | bool   | `true`               | Enable AI agent fallback                            |
| `extensions.resources.arm.apiVersionOverride` | string | (empty)              | Override ARM API version                            |
| `clients.cli.output.format`                   | string | `table`              | CLI output format: `json`, `table`, `tsv`, `yaml`   |
| `clients.powershell.output.format`            | string | `object`             | PowerShell output format: `object`, `json`, `table` |
| `defaults.group`                              | string | (empty)              | Default resource group                              |
| `defaults.location`                           | string | (empty)              | Default location                                    |
| `defaults.web`                                | string | (empty)              | Default web app name                                |
| `defaults.vm`                                 | string | (empty)              | Default VM name                                     |
| `defaults.vmss`                               | string | (empty)              | Default VMSS name                                   |
| `defaults.acr`                                | string | (empty)              | Default ACR name                                    |

## CLI Usage

### Get a configuration value

```bash
# Retrieve a single key
azv2 config get core.logging.level

# Output: core.logging.level = info [default]
```

### Set a configuration value

```bash
# Set a key (will prompt for confirmation)
azv2 config set core.logging.level debug

# Set with force (no prompt)
azv2 config set core.logging.level debug --force

# Output: Set core.logging.level = debug
```

### List all configuration

```bash
# List user config only
azv2 config list

# List with defaults (shows all keys with their current values)
azv2 config list --all

# Output as JSON
azv2 config list --all --json
```

### Delete a configuration value

```bash
# Delete a key (will prompt for confirmation)
azv2 config delete core.logging.level

# Delete with force (no prompt)
azv2 config delete core.logging.level --force

# Output: Deleted core.logging.level
```

### View schema information

```bash
# List all valid configuration keys
azv2 config info

# Output JSON schema (one key per line)
# agent.enabled
# core.logging.level
# core.http.maxRetries
# ...
```

## PowerShell Usage

### Import the Config module

```powershell
Import-Module ./src/PS/Config/Config.psd1
```

### Get a configuration value

```powershell
Get-CirrusConfig -Key core.logging.level
# Output: core.logging.level = info [default]

Get-CirrusConfig -Key core.logging.level -AsJson
# Output: {"key":"core.logging.level","value":"info","source":"default"}
```

### Set a configuration value

```powershell
# Set a key (will prompt for confirmation)
Set-CirrusConfig -Key core.logging.level -Value debug

# Set with force (no prompt)
Set-CirrusConfig -Key core.logging.level -Value debug -Force
```

### List all configuration

```powershell
# List user config only
Get-CirrusConfigList

# List with defaults
Get-CirrusConfigList -IncludeDefaults

# Output as JSON
Get-CirrusConfigList -IncludeDefaults -AsJson
```

### Delete a configuration value

```powershell
# Delete a key (will prompt for confirmation)
Remove-CirrusConfig -Key core.logging.level

# Delete with force (no prompt)
Remove-CirrusConfig -Key core.logging.level -Force
```

### View schema information

```powershell
# List all valid configuration keys
Get-CirrusConfigInfo

# Output only key names
Get-CirrusConfigInfo -KeysOnly
```

## Environment Variable Mappings

You can override configuration using environment variables:

| Environment Variable                                                | Maps to                      |
| ------------------------------------------------------------------- | ---------------------------- |
| `CIRRUS_LOG_LEVEL`                                                  | `core.logging.level`         |
| `PS_HTTP_MAX_RETRIES` / `AZURE_PS_HTTP_MAX_RETRIES`                 | `core.http.maxRetries`       |
| `PS_HTTP_MAX_RETRIES_FOR_429` / `AZURE_PS_HTTP_MAX_RETRIES_FOR_429` | `core.http.maxRetriesFor429` |
| `ZIPKIN_ENDPOINT`                                                   | `telemetry.zipkinEndpoint`   |

## Configuration File Location

Configuration is stored at:

- **Windows**: `%USERPROFILE%\.cirrus\config.json`
- **Linux/macOS**: `$HOME/.cirrus/config.json`

## Profile-aware Behavior (Current State)

Profile lifecycle commands are not part of this implementation, but config resolution is already profile-aware.

- The config service consumes an active profile context from `IProfileContextProvider`.
- If a provider returns an active profile file path and that file exists, values in that file override global config values.
- If no active profile exists (or the file is missing), config resolution falls back to global config and defaults.
- `set` and `delete` operations still mutate the global config file.

In command output, profile-origin values are labeled as `profile config`.

## Verifying Profile Handling Before Profile Commands Exist

You can validate behavior now with tests that simulate an active profile context.

### 1) Run profile-focused tests

```bash
dotnet test tests/Cli.Tests/Cli.Tests.csproj -c Release --filter "FullyQualifiedName~ConfigurationServiceTests.GetConfiguration_ShouldLayerActiveProfileOverGlobalConfig|FullyQualifiedName~ConfigurationServiceTests.GetConfiguration_ShouldFallbackToGlobal_WhenActiveProfileIsMissing"
```

Expected result:

- `GetConfiguration_ShouldLayerActiveProfileOverGlobalConfig` passes
- `GetConfiguration_ShouldFallbackToGlobal_WhenActiveProfileIsMissing` passes

### 2) Run defaults resolver test

```bash
dotnet test tests/Cli.Tests/Cli.Tests.csproj -c Release --filter "FullyQualifiedName~ConfigurationServiceTests.ResolveDefaultParameterAsync_ShouldReturnDefaultsValue_WhenConfigured"
```

Expected result:

- `defaults.*` values are resolved for parameter defaults when configured

### 3) Optional full config regression pass

```bash
dotnet test tests/Cli.Tests/Cli.Tests.csproj -c Release
```

This confirms profile layering, fallback behavior, defaults handling, and command-surface regressions as a bundle.

## Examples

### Workflow: Enable debug logging

**CLI:**

```bash
azv2 config set core.logging.level debug --force
azv2 config get core.logging.level
```

**PowerShell:**

```powershell
Set-CirrusConfig -Key core.logging.level -Value debug -Force
Get-CirrusConfig -Key core.logging.level
```

### Workflow: Check all settings with defaults

**CLI:**

```bash
azv2 config list --all
```

**PowerShell:**

```powershell
Get-CirrusConfigList -IncludeDefaults
```

### Workflow: Reset to defaults

**CLI:**

```bash
# Delete all user-set config
azv2 config list | grep -v '\[default\]' | awk '{print $1}' | while read key; do
  azv2 config delete "$key" --force
done
```

**PowerShell:**

```powershell
Get-CirrusConfigList | Where-Object { $_ -notmatch '\[default\]' } | ForEach-Object {
  $key = ($_ -split ' = ')[0]
  Remove-CirrusConfig -Key $key -Force
}
```
