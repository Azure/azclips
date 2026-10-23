---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Storage.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Storage
ms.date: 10/22/2026
PlatyPS schema version: 2024-05-01
title: Get-AzStorageFile
---

# Get-AzStorageFile

## SYNOPSIS

List native Azure Files children or retrieve one literal file/directory.

## SYNTAX

### ShareName (Default)

```
Get-AzStorageFile [-ShareName] <string> [[-Path] <string>] [-ExcludeExtendedInfo]
 [-Context <StorageContext>] [-EnableFileBackupRequestIntent] [-DisAllowTrailingDot]
 [-ShareSnapshot <string>] [-ServerTimeoutPerRequest <int>] [-Profile <string>] [<CommonParameters>]
```

### Share

```
Get-AzStorageFile [-ShareObject] <StorageFileShare> [[-Path] <string>] [-ExcludeExtendedInfo]
 [-Context <StorageContext>] [-EnableFileBackupRequestIntent] [-DisAllowTrailingDot]
 [-ShareSnapshot <string>] [-ServerTimeoutPerRequest <int>] [-Profile <string>] [<CommonParameters>]
```

### Directory

```
Get-AzStorageFile [-DirectoryObject] <StorageFileDirectory> [[-Path] <string>]
 [-ExcludeExtendedInfo] [-Context <StorageContext>] [-EnableFileBackupRequestIntent]
 [-DisAllowTrailingDot] [-ShareSnapshot <string>] [-ServerTimeoutPerRequest <int>]
 [-Profile <string>] [<CommonParameters>]
```

## ALIASES

None.


## DESCRIPTION

Uses a public-Azure, profile-backed Entra StorageContext with explicit Files backup request intent.
No keys, connection strings, SAS fallback, alternate clouds or SDK resource objects are accepted.
Supply a named share and Context, pipe a native StorageFileShare, or pipe a native StorageFileDirectory.
ShareClient and ShareDirectoryClient are compatibility parameter aliases for those native types, not SDK clients.

An omitted or empty Path lists all immediate children, draining service continuation pages in service order.
Both files and directories are returned as native pipeline resources. A nonempty Path is literal and relative
to the selected parent. It probes directory properties first, and tries file properties only after a 403 or 404
directory response. Other failures remain terminating errors. A selected directory is returned, not recursively listed.
ExcludeExtendedInfo applies only to listing and produces a warning when combined with an exact Path.
Trailing dots are preserved by default; DisAllowTrailingDot explicitly disables that behavior.

## EXAMPLES

### Example 1

List immediate children with explicit OAuth backup intent.

```powershell
$context = New-AzStorageContext -StorageAccountName account123 -EnableFileBackupRequestIntent
Get-AzStorageFile -ShareName documents -Context $context
```

### Example 2

Resolve a directory, then list its immediate children through the native pipeline.

```powershell
$context = New-AzStorageContext -StorageAccountName account123 -EnableFileBackupRequestIntent
$directory = Get-AzStorageFile -ShareName documents -Context $context -Path reports
$directory | Get-AzStorageFile -ExcludeExtendedInfo
```

## PARAMETERS

### -Context

Native Entra context; required at runtime for ShareName input. A native share/directory supplies its own context.
Any explicit context/profile must agree with that native resource.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageContext
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

### -DirectoryObject

Native parent directory. ShareDirectoryClient is an alias.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFileDirectory
DefaultValue: ''
SupportsWildcards: false
Aliases:
- ShareDirectoryClient
ParameterSets:
- Name: Directory
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -DisAllowTrailingDot

Disable preservation of trailing dots in Files names.

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

### -EnableFileBackupRequestIntent

Explicitly enable Files backup intent for this operation without mutating the supplied context.
Otherwise the context must already enable it. The caller needs appropriate Azure Files backup semantics RBAC.

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

### -ExcludeExtendedInfo

Exclude extended listing properties. Ignored with a warning for an exact Path.

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

### -Path

Literal relative path. Omitted or empty lists the selected parent's immediate children.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 1
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Profile

Named public-Azure profile; must agree with the native context.

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

### -ServerTimeoutPerRequest

Positive service timeout in seconds for each request.

```yaml
Type: System.Nullable`1[System.Int32]
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

### -ShareName

Share name used with Context.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ShareName
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ShareObject

Native share pipeline input. ShareClient is an alias.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFileShare
DefaultValue: ''
SupportsWildcards: false
Aliases:
- ShareClient
ParameterSets:
- Name: Share
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ShareSnapshot

Read-only share snapshot selector; must agree with a selected native snapshot.

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

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFileShare

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFileDirectory

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFile

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFileDirectory

## NOTES

Native resources preserve their account/profile, parent path and share snapshot.
No wildcard matching, recursion or unsupported-protocol fallback is performed.

## RELATED LINKS

- [New-AzStorageContext](New-AzStorageContext.md)
- [Get-AzStorageFileContent](Get-AzStorageFileContent.md)
- [New-AzStorageDirectory](New-AzStorageDirectory.md)
