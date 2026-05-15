---
summary: Delete a configuration value.
status: ga
schema: 1.0.0
online version:
---

Description: Remove a configuration setting by key. Once deleted, the setting reverts to its default value (if one exists). Unknown keys are rejected unless --force is specified.

### Delete a configuration setting (CLI)

```bash
azv2 config delete custom.mykey
```

### Force delete without validation (CLI)

```bash
azv2 config delete custom.mykey --force
```

### Delete a configuration value (PowerShell)

```powershell
Remove-CirrusConfig -Key "custom.mykey"
```

### Force delete (PowerShell)

```powershell
Remove-CirrusConfig -Key "custom.mykey" -Force
```
