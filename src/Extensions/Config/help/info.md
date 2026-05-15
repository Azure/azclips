---
summary: Display information about valid configuration keys.
status: ga
schema: 1.0.0
online version:
---

Description: Show metadata about available configuration keys including their descriptions, default values, and allowed value constraints. Use --keys-only to show only valid key names.

### Display configuration schema (CLI)

```bash
azv2 config info
```

### Display only valid configuration key names (CLI)

```bash
azv2 config info --keys-only
```

### Display configuration schema (PowerShell)

```powershell
Get-CirrusConfigInfo
```

### Display only key names (PowerShell)

```powershell
Get-CirrusConfigInfo -KeysOnly
```
