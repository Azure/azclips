Summary: Set or update a configuration value.

Description: Set a configuration value using dot notation keys. If the key already exists, it will be updated. Unknown keys are rejected unless --force is specified.

### Set output format to JSON

```bash
azv2 config set output.format json
```

### Set a custom configuration key with force flag

```bash
azv2 config set custom.mykey myvalue --force
```

### PowerShell: Set output format

```powershell
Set-CirrusConfig -Key "output.format" -Value "json"
```

### PowerShell: Set custom key with force

```powershell
Set-CirrusConfig -Key "custom.mykey" -Value "myvalue" -Force
```
