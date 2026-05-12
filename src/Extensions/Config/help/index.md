Summary: Manage local Cirrus configuration settings and preferences.

Description: View, set, update, or delete configuration values used by Cirrus. Configuration keys are organized in dot notation (e.g., core.debug, output.format). Configuration settings persist across CLI invocations and are stored in your local configuration file.

### Get a configuration value

```bash
azv2 config get output.format
```

### List all configuration settings

```bash
azv2 config list
```

### Set a configuration value

```bash
azv2 config set output.format json
```

### PowerShell: Get a configuration value

```powershell
Get-CirrusConfig -Key "output.format"
```

### PowerShell: List all configuration settings

```powershell
Get-CirrusConfigList
```

### PowerShell: Set a configuration value

```powershell
Set-CirrusConfig -Key "output.format" -Value "json"
```
