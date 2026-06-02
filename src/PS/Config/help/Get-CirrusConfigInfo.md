---
external help file: Microsoft.Azure.Cirrus.Cmdlets.Config.dll-Help.xml
Module Name: Config
online version:
schema: 2.0.0
---

# Get-CirrusConfigInfo

## SYNOPSIS

Display information about valid configuration keys.

## SYNTAX

```
Get-CirrusConfigInfo [-KeysOnly] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION

Show metadata about available configuration keys including their descriptions, default values, and allowed value constraints.
Use --keys-only to show only valid key names.

## EXAMPLES

### Example 1: Display configuration schema

```powershell
Get-CirrusConfigInfo
```

### Example 2: Display only key names

```powershell
Get-CirrusConfigInfo -KeysOnly
```

## PARAMETERS

### -KeysOnly

Return only valid keys.

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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

## NOTES

Generated help for config.

## RELATED LINKS
