---
external help file: Microsoft.Azure.Cirrus.Cmdlets.Resources.dll-Help.xml
Module Name: Resources
online version:
schema: 2.0.0
---

# Get-ResourceGroup

## SYNOPSIS

Show details of a specific resource group.

## SYNTAX

### List (Default)

```
Get-ResourceGroup -SubscriptionId <String> [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Get

```
Get-ResourceGroup -SubscriptionId <String> -Name <String> [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

## DESCRIPTION

Display information about a resource group including its name, location, provisioning state, and resource count.
Specify the resource group name and optionally the subscription ID (defaults to current subscription if not provided).

## EXAMPLES

### Example 1: Show a resource group

```powershell
Get-ResourceGroup -SubscriptionId "00000000-0000-0000-0000-000000000000" -Name "myresourcegroup"
```

### Example 2: List all resource groups in a subscription

```powershell
Get-ResourceGroup -SubscriptionId "00000000-0000-0000-0000-000000000000"
```

## PARAMETERS

### -Name

The name of the resource group.

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

Generated help for resources.

## RELATED LINKS
