---
summary: Set or update a configuration value.
status: ga
schema: 1.0.0
online version:
---

Description: Set a configuration value using dot notation keys. If the key already exists, it will be updated. Unknown keys are rejected unless --force is specified.

### Set output format to JSON (CLI)

```bash
azv2 config set output.format json
```

### Set a custom configuration key with force flag (CLI)

```bash
azv2 config set custom.mykey myvalue --force
```

### Set output format (PowerShell)

```powershell
Set-CirrusConfig -Key "output.format" -Value "json"
```

### Set custom key with force (PowerShell)

```powershell
Set-CirrusConfig -Key "custom.mykey" -Value "myvalue" -Force
```
