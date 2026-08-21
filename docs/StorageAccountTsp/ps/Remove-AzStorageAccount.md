---
external help file: Az.StorageAccount-Help.xml
Module Name: Az.StorageAccount
online version: https://learn.microsoft.com/powershell/module/az.storageaccount/remove-azstorageaccount
schema: 2.0.0
---

# Remove-AzStorageAccount

## SYNOPSIS

Delete a AzStorageAccount.

## SYNTAX

```
Remove-AzStorageAccount
    -SubscriptionId <string>
    -ResourceGroupName <string>
    -Name <string>
    [-Profile <string?>]
    [-Force <SwitchParameter>]
    [<CommonParameters>]
```

## DESCRIPTION

The **Remove-AzStorageAccount** cmdlet deletes a AzStorageAccount. This is a long-running operation; pass **-NoWait** to return before Azure finishes deleting.

## EXAMPLES

### Example 1: Delete a AzStorageAccount

```powershell
Remove-AzStorageAccount -SubscriptionId <sub> -ResourceGroupName <rg> -Name <name>
```

Note: long-running-operation. Pass `-NoWait` to return immediately without
waiting for deletion to finish.

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

### -SubscriptionId

Azure subscription ID (GUID) that owns the resource.

```yaml
Type: string
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResourceGroupName

Name of the resource group that contains the resource.

```yaml
Type: string
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Force

The **-Force** parameter.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name

The name of the AzStorageAccount.

```yaml
Type: string
Parameter Sets: (All)
Aliases:

Required: True
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
