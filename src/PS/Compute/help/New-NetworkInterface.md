---
external help file: Microsoft.Azure.Cirrus.Cmdlets.Compute.dll-Help.xml
Module Name: Compute
online version:
schema: 2.0.0
---

# New-NetworkInterface

## SYNOPSIS
Create a network interface attached to a virtual network and network security group.

## SYNTAX

```
New-NetworkInterface -SubscriptionId <String> -Name <String> -Location <String> -ResourceGroupName <String>
 -VirtualNetworkName <String> -NetworkSecurityGroupName <String> [-SubnetNames <String[]>]
 [-SubnetAddressPrefixes <String[][]>] [-AddressPrefixes <String[]>] [-RuleNames <String[]>]
 [-RuleDirections <String[]>] [-RuleProtocols <String[]>] [-RulePriorities <Int32[]>]
 [-RuleSourceAddressPrefixes <String[]>] [-RuleSourcePortRanges <String[]>]
 [-RuleDestinationPortRanges <String[]>] [-RuleDestinationAddressPrefixes <String[]>]
 [-RuleAccesses <String[]>] [-ProgressAction <ActionPreference>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Provisions a new network interface (NIC) in the specified resource group, wiring it to the named virtual network and network security group.
Optional parameters let you customize subnets, address prefixes, and NSG rules.

## EXAMPLES

### Example 1: Create a network interface
```
New-NetworkInterface -SubscriptionId "00000000-0000-0000-0000-000000000000" -Name "mynic" -Location "eastus" -ResourceGroupName "myrg" -VirtualNetworkName "myvnet" -NetworkSecurityGroupName "mynsg"
```

## PARAMETERS

### -AddressPrefixes
{{ Fill AddressPrefixes Description }}

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Location
{{ Fill Location Description }}

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

### -Name
{{ Fill Name Description }}

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

### -NetworkSecurityGroupName
{{ Fill NetworkSecurityGroupName Description }}

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
{{ Fill ResourceGroupName Description }}

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

### -RuleAccesses
{{ Fill RuleAccesses Description }}

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RuleDestinationAddressPrefixes
{{ Fill RuleDestinationAddressPrefixes Description }}

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RuleDestinationPortRanges
{{ Fill RuleDestinationPortRanges Description }}

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RuleDirections
{{ Fill RuleDirections Description }}

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RuleNames
{{ Fill RuleNames Description }}

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RulePriorities
{{ Fill RulePriorities Description }}

```yaml
Type: Int32[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RuleProtocols
{{ Fill RuleProtocols Description }}

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RuleSourceAddressPrefixes
{{ Fill RuleSourceAddressPrefixes Description }}

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RuleSourcePortRanges
{{ Fill RuleSourcePortRanges Description }}

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SubnetAddressPrefixes
{{ Fill SubnetAddressPrefixes Description }}

```yaml
Type: String[][]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SubnetNames
{{ Fill SubnetNames Description }}

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SubscriptionId
{{ Fill SubscriptionId Description }}

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

### -VirtualNetworkName
{{ Fill VirtualNetworkName Description }}

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

### -Confirm
Prompts you for confirmation before running the cmdlet.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: cf

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -WhatIf
Shows what would happen if the cmdlet runs.
The cmdlet is not run.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: wi

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None
## OUTPUTS

### System.Object
## NOTES

## RELATED LINKS
