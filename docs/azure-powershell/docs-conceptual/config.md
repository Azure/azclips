---
title: Configuration management
description: Learn how to manage Azure PowerShell private preview configuration settings, including keys, environment variables, and file locations.
---

# Configuration management

Azure PowerShell private preview stores user-scope configuration in a global JSON file.

Configuration follows strict precedence: **command args > environment variables > global config > defaults**.

## Valid Configuration Keys

| Key                                           | Type   | Default            | Notes                                                                                                                                                                                                                                                                   |
| --------------------------------------------- | ------ | ------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `core.logging.level`                          | string | `warn`             | Log level: `debug`, `info`, `warn`, `error`. Diagnostics (`warn`/`info`/`verbose`/`debug`) are written to **stderr**; command results stay on stdout.                                                                                                                   |
| `core.http.maxRetries`                        | int    | `3`                | Max HTTP request retries                                                                                                                                                                                                                                                |
| `core.http.maxRetriesFor429`                  | int    | `5`                | Max retries for 429 (rate limit) responses                                                                                                                                                                                                                              |
| `core.engine.extensionIndexCacheSize`         | int    | `512`              | LFU cache capacity for extension index (entry count)                                                                                                                                                                                                                    |
| `assemblyLoading.versionedInstallationDir`    | string | (system)           | Versioned assembly installation directory                                                                                                                                                                                                                               |
| `assemblyLoading.builtinInstallationDir`      | string | (system)           | Built-in assembly directory                                                                                                                                                                                                                                             |
| `assemblyLoading.extensionFolderPrefix`       | string | (see notes)        | Extension assembly folder prefix; `Microsoft.Azure.Azclips.Extensions.` (DEBUG), empty (Release)                                                                                                                                                                        |
| `telemetry.enabled`                           | bool   | `true`             | Enable telemetry                                                                                                                                                                                                                                                        |
| `extensions.resources.arm.apiVersionOverride` | string | (empty)            | Override ARM API version                                                                                                                                                                                                                                                |
| `clients.powershell.output.format`            | string | `object`           | Reserved for PowerShell output policy; cmdlets currently emit objects regardless of this value                                                                                                                                                                         |
| `defaults.group`                              | string | (empty)            | Default resource group                                                                                                                                                                                                                                                  |
| `defaults.location`                           | string | (empty)            | Default location                                                                                                                                                                                                                                                        |
| `defaults.web`                                | string | (empty)            | Default web app name                                                                                                                                                                                                                                                    |
| `defaults.vm`                                 | string | (empty)            | Default VM name                                                                                                                                                                                                                                                         |
| `defaults.vmss`                               | string | (empty)            | Default VMSS name                                                                                                                                                                                                                                                       |
| `defaults.acr`                                | string | (empty)            | Default ACR name                                                                                                                                                                                                                                                        |
| `survey.enabled`                              | bool   | `true`             | Controls if the reminder to fill a survey about Azure PowerShell is displayed |

## PowerShell Usage

### Logging thresholds

Configuration-aware PowerShell command streams, the CLI, and no-session background
logging share these baseline thresholds:

| Configured level | Enabled message severities |
| --- | --- |
| `error` | Error |
| `warn` | Error, Warning |
| `info` | Error, Warning, Information |
| `debug` | Error, Warning, Information, Debug, Verbose |

`verbose` is a message severity, not a configured level. Runtime level parsing
ignores casing and surrounding whitespace; missing or unrecognized values use
`warn`. Configuration commands still validate values against the supported levels.

`-Verbose` and `-Debug` enable their respective cmdlet streams independently of the
global baseline. Explicitly passing a false switch does not suppress messages
enabled by a globally configured `debug` level.

Configuration-aware cmdlets capture the baseline when their output streams are created. A level
change applies to subsequent invocations, not the command making the change.
No-session logging, including background module-import diagnostics, resolves the
global level for each non-error message and has no cmdlet switch override.
Errors bypass configuration; expected configuration-read failures in the fallback
use `warn`. A suppressed fallback diagnostic also produces no associated activity
log event.

### Import the Config module

```powershell
Import-Module ./src/PS/Config/Config.psd1
```

### Get configuration values or sections

```powershell
Get-AzConfig -Key core.logging.level

# Retrieve a section as objects with relative keys
Get-AzConfig -Key core

# Format objects as a table
Get-AzConfig -Key core | Format-Table

# Serialize objects as JSON
Get-AzConfig -Key core | ConvertTo-Json

# Filter structured output
Get-AzConfig -Key core | Where-Object Source -eq 'default'
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

# Export structured objects
Get-AzConfigList -IncludeDefaults | Export-Csv -NoTypeInformation config.csv
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

## Survey reminder

Azure PowerShell shows a survey reminder on the sixth executed command for a given version. 
The reminder is displayed on the warning stream. Use the following commands to turn it off and back on:

```powershell
Get-AzConfig -Key survey.enabled
Set-AzConfig -Key survey.enabled -Value false

# Turn the reminder back on
Set-AzConfig -Key survey.enabled -Value true
```

## Environment Variable Mappings

You can override configuration using environment variables:

| Environment Variable                                                | Maps to                      |
| ------------------------------------------------------------------- | ---------------------------- |
| `PS_HTTP_MAX_RETRIES` / `AZURE_PS_HTTP_MAX_RETRIES`                 | `core.http.maxRetries`       |
| `PS_HTTP_MAX_RETRIES_FOR_429` / `AZURE_PS_HTTP_MAX_RETRIES_FOR_429` | `core.http.maxRetriesFor429` |

## Configuration File Location

Configuration is stored at:

- **Windows**: `%USERPROFILE%\.azclips\config.json`
- **Linux/macOS**: `$HOME/.azclips/config.json`

## Examples

### Workflow: Enable debug logging

```powershell
Set-AzConfig -Key core.logging.level -Value debug -Force
Get-AzConfig -Key core.logging.level
```

### Workflow: Check all settings with defaults

```powershell
Get-AzConfigList -IncludeDefaults
```

### Workflow: Reset to defaults

```powershell
Get-AzConfigList | ForEach-Object {
  Remove-AzConfig -Key $_.Key -Force
}
```
