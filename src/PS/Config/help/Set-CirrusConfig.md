---
external help file: Microsoft.Azure.Cirrus.Cmdlets.Config.dll-Help.xml
Module Name: Config
online version:
schema: 2.0.0
---

# Set-CirrusConfig

## SYNOPSIS

Set or update a configuration value.

## SYNTAX

```
Set-CirrusConfig -Key <String> -Value <String> [-Force] [-ProgressAction <ActionPreference>] [-WhatIf]
 [-Confirm] [<CommonParameters>]
```

## DESCRIPTION

Set a configuration value using dot notation keys.
If the key already exists, it will be updated.
Unknown keys are rejected unless --force is specified.

## EXAMPLES

### Example 1: Set output format

```powershell
Set-CirrusConfig -Key "output.format" -Value "json"
```

### Example 2: Set custom key with force

```powershell
Set-CirrusConfig -Key "custom.mykey" -Value "myvalue" -Force
```

## PARAMETERS

### -Force

Allow unknown keys.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Key

Configuration key in dot notation.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProgressAction

{{ Fill ProgressAction Description }}

```yaml
Type: ActionPreference
Parameter Sets: (All)
Aliases: proga

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Value

Configuration value.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Confirm

Prompts you for confirmation before running the cmdlet.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: cf

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -WhatIf

Shows what would happen if the cmdlet runs.
The cmdlet is not run.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: wi

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

## NOTES

Generated help for config.

## RELATED LINKS
