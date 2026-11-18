---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Config.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Config
ms.date: 09/01/2026
PlatyPS schema version: 2024-05-01
title: Get-AzConfig
---

# Get-AzConfig

## SYNOPSIS

Retrieve effective configuration values, including defaults.

## SYNTAX

### List (Default)

```
Get-AzConfig [-ConfiguredOnly] [<CommonParameters>]
```

### Key

```
Get-AzConfig -Key <string> [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Without parameters, list all effective configuration settings, including built-in defaults for unset keys.
Environment variables override the configuration file, which overrides defaults.
Use `-ConfiguredOnly` to omit entries whose effective source is `default`.
File and environment values are retained even when equal to their default.
Specify `-Key` to get the current value of a configuration setting or section.
Configuration keys use dot notation (e.g., core.logging.level, defaults.location).
If the key is not found, an error is displayed.

## EXAMPLES

### List all effective settings

Include built-in defaults for unset keys.

```powershell
Get-AzConfig
```

### List only explicitly configured values

Include values configured in the configuration file or environment, without built-in defaults.

```powershell
Get-AzConfig -ConfiguredOnly
```

### Get a configuration value

Return the effective logging level, including any environment override.

```powershell
Get-AzConfig -Key core.logging.level
```

### Get a configuration section

Return structured entries whose keys are relative to the requested section.

```powershell
Get-AzConfig -Key core
```

## PARAMETERS

### -ConfiguredOnly

Show only values configured in the configuration file or environment. This switch cannot be combined with `-Key`.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: List
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Key

Configuration key or section in dot notation. Omit to list all effective settings.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Key
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Config.Models.ConfigEntry

One object per effective setting when listing, one configuration entry for an exact key, or configuration entries with relative keys when `-Key` identifies a section.

## NOTES

## RELATED LINKS

[Get-AzConfigList](Get-AzConfigList.md)
