---
document type: cmdlet
external help file: Microsoft.Azure.Cirrus.Cmdlets.Config.dll-Help.xml
HelpUri: ''
Locale: en-AU
Module Name: Config
ms.date: 06/03/2026
PlatyPS schema version: 2024-05-01
title: Get-CirrusConfig
---

# Get-CirrusConfig

## SYNOPSIS

Retrieve a configuration value.

## SYNTAX

### __AllParameterSets

```
Get-CirrusConfig -Key <string> [-AsJson] [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
  {{Insert list of aliases}}

## DESCRIPTION

Get the current value of a configuration setting by key.
Configuration keys use dot notation (e.g., output.format, core.debug).
If the key is not found, an error is displayed.

## EXAMPLES

### Get a configuration value

TODO - describe the example.

```powershell
Get-CirrusConfig -Key <key>
```

### Get output as JSON

TODO - describe the example.

```powershell
Get-CirrusConfig -Key <key>
```

## PARAMETERS

### -AsJson

{{ Fill AsJson Description }}

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

### -Key

Configuration key in dot notation.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
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

## NOTES

Generated help for config.


## RELATED LINKS



- [Online Version]()
