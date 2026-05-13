Summary: Display information about valid configuration keys.

Description: Show metadata about available configuration keys including their descriptions, default values, and allowed value constraints. Use --keys-only to show only valid key names.

### Display configuration schema

```bash
azv2 config info
```

### Display only valid configuration key names

```bash
azv2 config info --keys-only
```

### PowerShell: Display configuration schema

```powershell
Get-CirrusConfigInfo
```

### PowerShell: Display only key names

```powershell
Get-CirrusConfigInfo -KeysOnly
```
