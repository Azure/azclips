---
external help file: Microsoft.Azure.Cirrus.Cmdlets.Compute.dll-Help.xml
Module Name: Compute
online version:
schema: 2.0.0
---

# Get-VM

## SYNOPSIS
Show details for a virtual machine.

## SYNTAX

### List (Default)
```
Get-VM -SubscriptionId <String> -ResourceGroupName <String> [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

### Get
```
Get-VM -SubscriptionId <String> -ResourceGroupName <String> -Name <String> [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

## DESCRIPTION
Retrieves the properties of a single virtual machine identified by name within the specified resource group and subscription.

## EXAMPLES

### Example 1: Get a VM by name
```
Get-VM -SubscriptionId "00000000-0000-0000-0000-000000000000" -ResourceGroupName "myrg" -Name "myvm"
```

### Example 2: List VMs in a resource group
```
Get-VM -SubscriptionId "00000000-0000-0000-0000-000000000000" -ResourceGroupName "myrg"
```

## PARAMETERS

### -Name
The name of the virtual machine.

```yaml
Type: String
Parameter Sets: Get
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProgressAction
{{ Fill ProgressAction Description }}

```yaml
Type: ActionPreference
Parameter Sets: (All)
Aliases: proga

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResourceGroupName
The resource group name.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SubscriptionId
Azure Subscription Id.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

## NOTES
Generated help for compute. Daniel

## RELATED LINKS
