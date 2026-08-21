---
external help file: Az.StorageAccount-Help.xml
Module Name: Az.StorageAccount
online version: https://learn.microsoft.com/powershell/module/az.storageaccount/get-azstorageaccount
schema: 2.0.0
---

# Get-AzStorageAccount

## SYNOPSIS

Get details of a AzStorageAccount.

## SYNTAX

```
Get-AzStorageAccount
    [-Profile <string?>]
    [-BodyJsonPatch <string?>]
    [<CommonParameters>]
```

## DESCRIPTION

The **Get-AzStorageAccount** cmdlet retrieves a AzStorageAccount from Azure Resource Manager. If a **-Name** is specified, the cmdlet returns a single resource; otherwise it returns all AzStorageAccount resources in the given resource group.

## EXAMPLES

### Example 1: Get a specific AzStorageAccount

```powershell
Get-AzStorageAccount -SubscriptionId <sub> -ResourceGroupName <rg> -Name <name>
```

Retrieves the specified AzStorageAccount by name.

### Example 2: List all AzStorageAccount resources in a resource group

```powershell
Get-AzStorageAccount -SubscriptionId <sub> -ResourceGroupName <rg>
```

## PARAMETERS

### -Profile

Use the named profile for this command instead of the active profile.

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BodyJsonPatch

Raw JSON deep-merged into the request body after flat parameters. Escape hatch for shapes the tsp emitter cannot flatten (e.g. arrays of nested models).

```yaml
Type: string?
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable, -ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

## NOTES

## RELATED LINKS

- [Az.StorageAccount Module](Az.StorageAccount.md)
