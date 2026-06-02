---
external help file: Microsoft.Azure.Cirrus.Cmdlets.Config.dll-Help.xml
Module Name: Config
online version:
schema: 2.0.0
---

# Get-CirrusConfig

## SYNOPSIS

Retrieve a configuration value.

## SYNTAX

```
Get-CirrusConfig -Key <String> [-AsJson] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION

Get the current value of a configuration setting by key.
Configuration keys use dot notation (e.g., output.format, core.debug).
If the key is not found, an error is displayed.

## EXAMPLES

### Example 1: Get a configuration value

```powershell
Get-CirrusConfig -Key "output.format"
```

### Example 2: Get output as JSON

```powershell
Get-CirrusConfig -Key "output.format" -AsJson
```

## PARAMETERS

### -AsJson

{{ Fill AsJson Description }}

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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

## NOTES

Generated help for config.

## RELATED LINKS
