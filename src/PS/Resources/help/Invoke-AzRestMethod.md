---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 10-09-2026
PlatyPS schema version: 2024-05-01
title: Invoke-AzRestMethod
---

# Invoke-AzRestMethod

## SYNOPSIS

Send an authenticated REST request to Azure Resource Manager and return the response payload.

## SYNTAX

### __AllParameterSets

```
Invoke-AzRestMethod [-Method <string>] [-Uri <string>] [-Payload <string>] [-Headers <Object>]
 [-SubscriptionId <string>] [-ResourceGroupName <string>] [-ResourceProviderName <string>]
 [-ResourceType <string[]>] [-Name <string[]>] [-ApiVersion <string>] [-UriParameters <Object>]
 [-FullResponse] [-Profile <string>] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

`Invoke-AzRestMethod` is a universal authenticated ARM REST passthrough. It sends an arbitrary HTTP request through the already-authenticated AzClips pipeline to Azure Resource Manager and returns the parsed JSON payload as native PowerShell objects by default. JSON is recognized regardless of content type. Non-JSON and malformed JSON bodies are returned as raw strings; malformed content advertised as JSON also produces a diagnostic. Empty bodies emit no output.

Use `-FullResponse` to return a `RestResponse` envelope containing the HTTP status code, response headers, raw `Content`, and parsed `Json`. This changes azclips' previous envelope default and differs from Azure PowerShell's `Invoke-AzRestMethod`, which returns an envelope by default. Existing scripts accessing `.StatusCode`, `.Headers`, `.Content`, or `.Json` must add `-FullResponse`.

PowerShell displays and pipelines native objects, not serialized JSON text. Use `ConvertTo-Json -Depth 100` for prettified JSON text or add `-Compress` for compact text. CLI `az rest` automatically prettifies JSON/JSONC in a terminal and compacts it when piped or redirected.

The target URL can be supplied two ways. In **Uri mode**, pass `-Uri` with an absolute ARM URL or a relative ARM path (relative paths are prefixed with `https://management.azure.com`, and the token `{subscriptionId}` is replaced with the current subscription). In **ByParameters mode**, build the URL piece-wise from `-SubscriptionId`, `-ResourceGroupName`, `-ResourceProviderName`, `-ResourceType`, `-Name`, and `-ApiVersion`. The two modes are mutually exclusive.

This cmdlet performs no long-running-operation polling and no automatic pagination, and it targets the Azure Resource Manager audience only. Use it to call ARM endpoints that do not yet have a dedicated cmdlet.

## EXAMPLES

### Example 1: List resource groups in a subscription (Uri mode)

Send a `GET` to an absolute ARM path and return the parsed payload.

```powershell
Invoke-AzRestMethod -Method GET -Uri "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01"
```

### Example 2: Get a resource group built from parameters (ByParameters mode)

Build the ARM URL piece-wise instead of passing a full URI. The default method is `GET`.

```powershell
Invoke-AzRestMethod -ResourceGroupName myRg -ApiVersion 2021-04-01
```

### Example 3: Create or update a resource group with a JSON payload

Send a `PUT` with a request body and a custom header.

```powershell
Invoke-AzRestMethod -Method PUT -Uri "/subscriptions/{subscriptionId}/resourcegroups/myRg?api-version=2021-04-01" -Payload '{"location":"eastus"}' -Headers @{ 'Content-Type' = 'application/json' }
```

### Example 4: Inspect the full HTTP response

```powershell
$response = Invoke-AzRestMethod -Uri "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01" -FullResponse
$response.StatusCode
$response.Headers
$response.Json
```

## PARAMETERS

### -ApiVersion

The `api-version` used when building an ARM URL from piece-wise parameters. Required in ByParameters mode. Mutually exclusive with `-Uri`.

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

### -FullResponse

Return the full HTTP response envelope instead of only the payload.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: False
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

### -Headers

Optional request headers, supplied either as a hashtable (`@{ 'Accept' = 'application/json' }`) or as a string array in `KEY=VALUE` form (`'Accept=application/json'`).

```yaml
Type: System.Object
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

### -Method

The HTTP method to use (`GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `HEAD`, or `OPTIONS`). Defaults to `GET`.

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

### -Name

Resource name segments, interleaved with `-ResourceType` (may have one fewer entry than `-ResourceType` for a collection GET). Used to build an ARM URL from piece-wise parameters. Mutually exclusive with `-Uri`.

```yaml
Type: System.String[]
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

### -Payload

Optional request body (typically JSON). Sent whenever provided, for any HTTP method.

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

### -Profile

Use the named profile for this command, overriding the active profile.

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

### -ResourceGroupName

Resource group name segment used to build an ARM URL from piece-wise parameters. Mutually exclusive with `-Uri`.

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

### -ResourceProviderName

Resource provider namespace (for example, `Microsoft.Resources`) used to build an ARM URL from piece-wise parameters. Mutually exclusive with `-Uri`.

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

### -ResourceType

Resource type segments, interleaved with `-Name`, used to build an ARM URL from piece-wise parameters. Mutually exclusive with `-Uri`.

```yaml
Type: System.String[]
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

Subscription id used to build an ARM URL from piece-wise parameters. Defaults to the current subscription. Mutually exclusive with `-Uri`.

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

### -Uri

Absolute URL or relative ARM path for the request. Relative paths are prefixed with `https://management.azure.com`, and the token `{subscriptionId}` is replaced with the current subscription. Mutually exclusive with the piece-wise ByParameters inputs (`-SubscriptionId`, `-ResourceGroupName`, `-ResourceProviderName`, `-ResourceType`, `-Name`, `-ApiVersion`).

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

### -UriParameters

Optional query-string parameters, supplied either as a hashtable (`@{ '$top' = '3' }`) or as a string array in `KEY=VALUE` form (`'$top=3'`). Appended to the final URL in either mode.

```yaml
Type: System.Object
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

### System.Object

Native PowerShell objects, arrays, or scalar values for JSON payloads; raw strings for non-JSON bodies. Top-level arrays are enumerated into the pipeline. JSON null is represented as PowerShell null.

### Microsoft.Azure.Azclips.Extensions.Resources.Models.RestResponse

With `-FullResponse`, a `RestResponse` object containing the HTTP status code, response headers, parsed JSON body (when applicable), and raw body text.

## NOTES

This cmdlet targets the Azure Resource Manager audience only. It does not poll long-running operations and does not page through `nextLink` results; for paged endpoints, follow the returned `nextLink` yourself with another call.

## RELATED LINKS


- [Get-AzResourceGroup]()
- [New-AzResourceGroupDeployment]()
