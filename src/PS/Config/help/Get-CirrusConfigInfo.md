---
document type: cmdlet
external help file: Microsoft.Azure.Cirrus.Cmdlets.Config.dll-Help.xml
HelpUri: ''
Locale: en-AU
Module Name: Config
ms.date: 06/03/2026
PlatyPS schema version: 2024-05-01
title: Get-CirrusConfigInfo
---

# Get-CirrusConfigInfo

## SYNOPSIS

Display information about valid configuration keys.

## SYNTAX

### __AllParameterSets

```
Get-CirrusConfigInfo [-KeysOnly] [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
  {{Insert list of aliases}}

## DESCRIPTION

Show metadata about available configuration keys including their descriptions, default values, and allowed value constraints.
Use --keys-only to show only valid key names.

## EXAMPLES

### Display configuration schema

TODO - describe the example.

```powershell
Get-CirrusConfigInfo
```

### Display only key names

TODO - describe the example.

```powershell
Get-CirrusConfigInfo
```

## PARAMETERS

### -KeysOnly

Return only valid keys.

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

## NOTES

Generated help for config.


## RELATED LINKS



- [Online Version]()
