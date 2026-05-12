Summary: List all configuration settings.

Description: Display all current configuration values. By default, only explicitly-set values are shown. Use --all to include default values. Output can be formatted as JSON for scripting.

### List current configuration values

```bash
azv2 config list
```

### List all configuration including defaults

```bash
azv2 config list --all
```

### List configuration as JSON

```bash
azv2 config list --json
```

### PowerShell: List all configuration

```powershell
Get-CirrusConfigList
```

### PowerShell: List with defaults

```powershell
Get-CirrusConfigList -All
```

### PowerShell: List as JSON

```powershell
Get-CirrusConfigList -Json
```
