---
summary: List all configuration settings.
status: ga
schema: 1.0.0
online version:
---

Description: Display all current configuration values. By default, only explicitly-set values are shown. Use --all to include default values. Output can be formatted as JSON for scripting.

### List current configuration values (CLI)

```bash
azv2 config list
```

### List all configuration including defaults (CLI)

```bash
azv2 config list --all
```

### List configuration as JSON (CLI)

```bash
azv2 config list --json
```

### List all configuration (PowerShell)

```powershell
Get-CirrusConfigList
```

### List with defaults (PowerShell)

```powershell
Get-CirrusConfigList -IncludeDefaults
```

### List as JSON (PowerShell)

```powershell
Get-CirrusConfigList -AsJson
```
