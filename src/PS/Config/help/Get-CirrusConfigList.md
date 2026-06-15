---
document type: cmdlet
external help file: Microsoft.Azure.Cirrus.Cmdlets.Config.dll-Help.xml
HelpUri: ""
Locale: en-US
Module Name: Config
ms.date: 06/03/2026
PlatyPS schema version: 2024-05-01
title: Get-CirrusConfigList
---

# Get-CirrusConfigList

## SYNOPSIS

List all configuration settings.

## SYNTAX

### \_\_AllParameterSets

```
Get-CirrusConfigList [-IncludeDefaults] [-AsJson] [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
{{Insert list of aliases}}

## DESCRIPTION

Display all current configuration values.
By default, only explicitly-set values are shown.
Use --all to include default values.
Output can be formatted as JSON for scripting.

## EXAMPLES

### List all configuration

Display every explicitly-set configuration value.

```powershell
Get-CirrusConfigList
```

### List with defaults

Include default values for keys that have not been explicitly set.

```powershell
Get-CirrusConfigList -IncludeDefaults
```

### List as JSON

Format the output as JSON for downstream scripting.

```powershell
Get-CirrusConfigList -AsJson
```

## PARAMETERS

### -AsJson

{{ Fill AsJson Description }}

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ""
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
HelpMessage: ""
```

### -IncludeDefaults

{{ Fill IncludeDefaults Description }}

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ""
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
HelpMessage: ""
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
