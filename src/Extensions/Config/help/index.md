---
summary: Manage local Cirrus configuration settings and preferences.
status: ga
schema: 1.0.0
online version:
---

Description: View, set, update, or delete configuration values used by Cirrus. Configuration keys are organized in dot notation (e.g., core.debug, output.format). Configuration settings persist across CLI invocations and are stored in your local configuration file.

### Get a configuration value (CLI)

```bash
azv2 config get output.format
```

### List all configuration settings (CLI)

```bash
azv2 config list
```

### Set a configuration value (CLI)

```bash
azv2 config set output.format json
```

### Get a configuration value (PowerShell)

```powershell
Get-CirrusConfig -Key "output.format"
```

### List all configuration settings (PowerShell)

```powershell
Get-CirrusConfigList
```

### Set a configuration value (PowerShell)

```powershell
Set-CirrusConfig -Key "output.format" -Value "json"
```
