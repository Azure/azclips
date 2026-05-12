Summary: Retrieve a configuration value.

Description: Get the current value of a configuration setting by key. Configuration keys use dot notation (e.g., output.format, core.debug). If the key is not found, an error is displayed.

### Get output format setting

```bash
azv2 config get output.format
```

### Get output as JSON

```bash
azv2 config get output.format --json
```

### PowerShell: Get a configuration value

```powershell
Get-CirrusConfig -Key "output.format"
```

### PowerShell: Get output as JSON

```powershell
Get-CirrusConfig -Key "output.format" -Json
```
