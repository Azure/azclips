---
external help file: Microsoft.Azure.Cirrus.Cmdlets.Config.dll-Help.xml
Module Name: Config
online version:
schema: 2.0.0
---

# Get-CirrusConfigList

## SYNOPSIS

List all configuration settings.

## SYNTAX

```
Get-CirrusConfigList [-IncludeDefaults] [-AsJson] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION

Display all current configuration values.
By default, only explicitly-set values are shown.
Use --all to include default values.
Output can be formatted as JSON for scripting.

## EXAMPLES

### Example 1: List all configuration

```powershell
Get-CirrusConfigList
```

### Example 2: List with defaults

```powershell
Get-CirrusConfigList -IncludeDefaults
```

### Example 3: List as JSON

```powershell
Get-CirrusConfigList -AsJson
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

### -IncludeDefaults

{{ Fill IncludeDefaults Description }}

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
