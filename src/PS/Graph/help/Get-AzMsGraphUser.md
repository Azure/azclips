---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Graph.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Graph
ms.date: 07/20/2026
PlatyPS schema version: 2024-05-01
title: Get-AzMsGraphUser
---

# Get-AzMsGraphUser

## SYNOPSIS

Gets Microsoft Graph users. Lists users in the signed-in tenant, retrieves a single user by object ID or user principal name, or returns the currently signed-in user.

## SYNTAX

### List (Default)

```
Get-AzMsGraphUser [-Filter <string>] [-Orderby <string[]>] [-Search <string>]
 [-ConsistencyLevel <string>] [-Select <string[]>] [-Expand <string[]>] [-AppendSelected]
 [<CommonParameters>]
```

### Mail

```
Get-AzMsGraphUser -Mail <string> [-Select <string[]>] [-Expand <string[]>] [-AppendSelected]
 [<CommonParameters>]
```

### DisplayName

```
Get-AzMsGraphUser -DisplayName <string> [-Select <string[]>] [-Expand <string[]>]
 [-AppendSelected] [<CommonParameters>]
```

### StartsWith

```
Get-AzMsGraphUser -StartsWith <string> [-Select <string[]>] [-Expand <string[]>]
 [-AppendSelected] [<CommonParameters>]
```

### Get

```
Get-AzMsGraphUser -Id <string> [-Select <string[]>] [-Expand <string[]>] [-AppendSelected]
 [<CommonParameters>]
```

### SignedIn

```
Get-AzMsGraphUser -SignedIn [-Select <string[]>] [-Expand <string[]>] [-AppendSelected]
 [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Queries Microsoft Graph for user objects using the active Azure profile (see `Connect-AzAccount`). The parameter surface mirrors the non-pagination subset of `Get-AzADUser`:

- **List (Default)**: `GET /users` — returns the first page of users. Combine with `-Filter`, `-Orderby`, `-Search`, and `-ConsistencyLevel` for raw OData queries. Pagination via `@odata.nextLink` is not followed in this preview.
- **Mail / DisplayName / StartsWith**: shortcuts that translate to `-Filter` before invoking Graph — respectively `mail eq '…'`, `displayName eq '…'`, `startswith(displayName,'…')`.
- **Get**: `GET /users/{id}` — retrieves a single user by object ID or user principal name.
- **SignedIn**: `GET /me` — returns the currently signed-in user.

`-Select` and `-Expand` control OData projection and are available in every parameter set. `-AppendSelected` widens `-Select` to include the default Graph user property set (`displayName`, `id`, `deletedDateTime`, `userPrincipalName`, `usageLocation`, `givenName`, `surname`, `accountEnabled`, `mailNickname`, `mail`) instead of replacing it.

## EXAMPLES

### Example 1: List the first page of users

```powershell
Get-AzMsGraphUser
```

### Example 2: List users by exact mail address

```powershell
Get-AzMsGraphUser -Mail ada@contoso.com
```

### Example 3: List users by exact display name

```powershell
Get-AzMsGraphUser -DisplayName 'Ada Lovelace'
```

### Example 4: List users whose display name starts with a prefix

```powershell
Get-AzMsGraphUser -StartsWith Ada
```

### Example 5: Filter users with a raw OData expression

```powershell
Get-AzMsGraphUser -Filter "endswith(mail,'@contoso.com')" -ConsistencyLevel eventual
```

### Example 6: Get a user by object ID or user principal name

```powershell
Get-AzMsGraphUser -Id 00000000-0000-0000-0000-000000000000
```

### Example 7: Get the currently signed-in user

```powershell
Get-AzMsGraphUser -SignedIn
```

### Example 8: Project a subset of properties, widened with the defaults

```powershell
Get-AzMsGraphUser -Select 'city','department' -AppendSelected
```

## PARAMETERS

### -Id

The object ID or user principal name (UPN) of the Microsoft Graph user to retrieve.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Get
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Mail

Exact mail address to match. Translated to the OData filter `mail eq '<value>'`.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Mail
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -DisplayName

Exact display name to match. Translated to the OData filter `displayName eq '<value>'`.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: DisplayName
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -StartsWith

Prefix to match against the display name. Translated to the OData filter `startswith(displayName,'<value>')`.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: StartsWith
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -SignedIn

When set, returns the currently signed-in user via Microsoft Graph `GET /me`.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: 'False'
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: SignedIn
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Filter

OData `$filter` expression applied by Microsoft Graph. Passed through verbatim; Graph validates the syntax and returns a 400 with details on invalid expressions.

```yaml
Type: System.String
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

### -Orderby

Property paths to sort by via OData `$orderby` (for example, `displayName` or `mail desc`).

```yaml
Type: System.String[]
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

### -Search

Free-text search expression mapped to OData `$search` (for example, `"displayName:ada"`).

```yaml
Type: System.String
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

### -ConsistencyLevel

Value sent as the `ConsistencyLevel` HTTP header. Set to `eventual` when using advanced queries such as `$search`, `$count`, or `$orderby` on non-indexed properties.

```yaml
Type: System.String
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

### -Select

Properties to project via OData `$select`. Combine with `-AppendSelected` to widen rather than replace the default property set.

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

### -Expand

Related navigation properties to expand via OData `$expand` (for example, `memberOf`).

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

### -AppendSelected

When set, prepends the default Microsoft Graph user property set (`displayName`, `id`, `deletedDateTime`, `userPrincipalName`, `usageLocation`, `givenName`, `surname`, `accountEnabled`, `mailNickname`, `mail`) to the values supplied via `-Select` instead of replacing them.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: 'False'
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

In the list-oriented parameter sets (List, Mail, DisplayName, StartsWith), each Microsoft Graph user object from the first response page is emitted individually to the pipeline. In the Get and SignedIn parameter sets, a single user object is returned.

## NOTES

## RELATED LINKS
