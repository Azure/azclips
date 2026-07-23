---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Graph.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Graph
ms.date: 07/22/2026
PlatyPS schema version: 2024-05-01
title: Test-AzMsGraphGroupMembership
---

# Test-AzMsGraphGroupMembership

## SYNOPSIS

Checks whether a directory object is a member of a Microsoft Entra group.

## SYNTAX

### __AllParameterSets

```
Test-AzMsGraphGroupMembership -Group <string> -MemberId <string> [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Report whether the specified member (user, group, service principal, or device) is a transitive member of the given group. The group can be identified by object ID or display name (prefix works if there is a unique match). Emits `{Value = true}` when the member is in the group, `{Value = false}` otherwise.

## EXAMPLES

### Example 1: Check membership by group object ID

```powershell
Test-AzMsGraphGroupMembership -Group 11111111-2222-3333-4444-555555555555 -MemberId aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee
```

### Example 2: Check membership by group display name

```powershell
Test-AzMsGraphGroupMembership -Group Sales -MemberId aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee
```

## PARAMETERS

### -Group

The group's object ID or display name (prefix works if there is a unique match).

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

### -MemberId

The object ID of the member to test.

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

### System.Object

An object with a single `Value` property that is `true` if the member is in the group, `false` otherwise.

## NOTES

## RELATED LINKS
