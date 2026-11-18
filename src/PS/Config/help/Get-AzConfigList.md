---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Config.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Config
ms.date: 09/01/2026
PlatyPS schema version: 2024-05-01
title: Get-AzConfigList
---

# Get-AzConfigList

## SYNOPSIS

List all configuration settings.

## SYNTAX

### __AllParameterSets

```
Get-AzConfigList [-ConfiguredOnly] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Display all current configuration values.
By default, built-in defaults are included for unset keys.
Environment variables override the configuration file, which overrides defaults.
Use `-ConfiguredOnly` to omit entries whose source is `default`.
File and environment values are retained even when equal to their default.
Each configuration entry is emitted as a structured object.
You can also use `Get-AzConfig` to list settings.

## EXAMPLES

### List all configuration

Display every effective configuration value, including defaults for unset keys.

```powershell
Get-AzConfigList
```

### List only explicitly configured values

Include only values configured in the configuration file or environment.

```powershell
Get-AzConfigList -ConfiguredOnly
```

## PARAMETERS

### -ConfiguredOnly

Show only values configured in the configuration file or environment, omitting entries whose effective source is `default`.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
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

One object per effective entry, including defaults unless `-ConfiguredOnly` is specified.

## NOTES

Defaults are now included automatically. Replace `Get-AzConfigList -IncludeDefaults` with `Get-AzConfigList` or `Get-AzConfig`. To retain the previous configured-only behavior, use `-ConfiguredOnly`.

## RELATED LINKS

[Get-AzConfig](Get-AzConfig.md)
