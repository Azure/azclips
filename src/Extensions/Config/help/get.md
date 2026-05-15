---
summary: Retrieve a configuration value.
status: ga
schema: 1.0.0
online version:
---

Description: Get the current value of a configuration setting by key. Configuration keys use dot notation (e.g., output.format, core.debug). If the key is not found, an error is displayed.

### Get output format setting (CLI)

```bash
azv2 config get output.format
```

### Get output as JSON (CLI)

```bash
azv2 config get output.format --json
```

### Get a configuration value (PowerShell)

```powershell
Get-CirrusConfig -Key "output.format"
```

### Get output as JSON (PowerShell)

```powershell
Get-CirrusConfig -Key "output.format" -AsJson
```
