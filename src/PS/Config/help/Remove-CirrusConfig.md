---
external help file: Microsoft.Azure.Cirrus.Cmdlets.Config.dll-Help.xml
Module Name: Config
online version:
schema: 2.0.0
---

# Remove-CirrusConfig

## SYNOPSIS

Delete a configuration value.

## SYNTAX

```
Remove-CirrusConfig -Key <String> [-Force] [-ProgressAction <ActionPreference>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## DESCRIPTION

Remove a configuration setting by key.
Once deleted, the setting reverts to its default value (if one exists).
Unknown keys are rejected unless --force is specified.

## EXAMPLES

### Example 1: Delete a configuration value

```powershell
Remove-CirrusConfig -Key "custom.mykey"
```

### Example 2: Force delete

```powershell
Remove-CirrusConfig -Key "custom.mykey" -Force
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
