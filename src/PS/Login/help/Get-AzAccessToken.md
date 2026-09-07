---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Login.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Login
ms.date: 09/07/2026
PlatyPS schema version: 2024-05-01
title: Get-AzAccessToken
---

# Get-AzAccessToken

## SYNOPSIS

Gets an access token for the current signed-in context.

## SYNTAX

### ResourceType (Default)

```
Get-AzAccessToken [-ResourceTypeName <string>] [-TenantId <string>] [-SubscriptionId <string>] [<CommonParameters>]
```

### Resource

```
Get-AzAccessToken -ResourceUrl <string> [-TenantId <string>] [-SubscriptionId <string>] [<CommonParameters>]
```

### Scope

```
Get-AzAccessToken -Scope <string[]> [-TenantId <string>] [-SubscriptionId <string>] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Acquires an access token from the current signed-in context for a resource, a well-known resource type, or an explicit list of Microsoft Entra scopes. When no target is specified, a token for Azure Resource Manager (ARM) is returned.

The token is protected by default and is not emitted as plain text. To emit the raw token as plain text, set the `AZUREPS_OUTPUT_PLAINTEXT_AZACCESSTOKEN` environment variable before running the command.

## EXAMPLES

### Example 1

Gets an access token for Azure Resource Manager (the default target).

```powershell
Get-AzAccessToken
```

### Example 2

Gets an access token for a well-known resource type using its shortcut name.

```powershell
Get-AzAccessToken -ResourceTypeName MSGraph
```

### Example 3

Gets an access token for a specific resource URL.

```powershell
Get-AzAccessToken -ResourceUrl https://vault.azure.net
```

### Example 4

Gets an access token for an explicit list of Microsoft Entra v2.0 scopes.

```powershell
Get-AzAccessToken -Scope 'https://graph.microsoft.com/.default'
```

## PARAMETERS

### -ResourceUrl

The resource URL (Microsoft Entra v1.0) to acquire the token for, for example `https://vault.azure.net`. Mutually exclusive with -ResourceTypeName and -Scope.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Resource
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ResourceTypeName

A well-known resource type shortcut, for example `Arm`, `MSGraph`, or `KeyVault`. Mutually exclusive with -ResourceUrl and -Scope.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ResourceType
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Scope

One or more Microsoft Entra v2.0 scopes. Mutually exclusive with -ResourceUrl and -ResourceTypeName.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Scope
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -TenantId

The tenant ID to acquire the token for. Defaults to the current profile's tenant.

```yaml
Type: System.String
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

### -SubscriptionId

The subscription ID associated with the request. Defaults to the current profile's subscription.

```yaml
Type: System.String
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

### Core.Abstractions.AccessTokenResult

The acquired token result: the token (protected by default), its expiry, the tenant ID, the subscription ID, and the token type.

## NOTES

The token is protected by default. Set the `AZUREPS_OUTPUT_PLAINTEXT_AZACCESSTOKEN` environment variable to emit the raw token as plain text.

## RELATED LINKS
