Summary: Delete a configuration value.

Description: Remove a configuration setting by key. Once deleted, the setting reverts to its default value (if one exists). Unknown keys are rejected unless --force is specified.

### Delete a configuration setting

```bash
azv2 config delete custom.mykey
```

### Force delete without validation

```bash
azv2 config delete custom.mykey --force
```

### PowerShell: Delete a configuration value

```powershell
Remove-CirrusConfig -Key "custom.mykey"
```

### PowerShell: Force delete

```powershell
Remove-CirrusConfig -Key "custom.mykey" -Force
```
