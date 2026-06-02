---
external help file: Microsoft.Azure.Cirrus.Cmdlets.Compute.dll-Help.xml
Module Name: Compute
online version:
schema: 2.0.0
---

# New-VM

## SYNOPSIS

Create a virtual machine together with its network interface, virtual network, and network security group in a single call.

## SYNTAX

```
New-VM -SubscriptionId <String> -Name <String> -Location <String> -ResourceGroupName <String> -VmSize <String>
 -AdminUsername <String> -AdminPassword <String> -NetworkInterfaceName <String> -VirtualNetworkName <String>
 -NetworkSecurityGroupName <String> [-SubnetNames <String[]>] [-SubnetAddressPrefixes <String[][]>]
 [-AddressPrefixes <String[]>] [-RuleNames <String[]>] [-RuleDirections <String[]>] [-RuleProtocols <String[]>]
 [-RulePriorities <Int32[]>] [-RuleSourceAddressPrefixes <String[]>] [-RuleSourcePortRanges <String[]>]
 [-RuleDestinationPortRanges <String[]>] [-RuleDestinationAddressPrefixes <String[]>]
 [-RuleAccesses <String[]>] [-ProgressAction <ActionPreference>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION

Provisions the full networking + VM stack as a single operation.
Creates a virtual network, network security group, network interface, and virtual machine using the supplied names and parameters.
Use this command when you want a one-shot VM deployment without composing the underlying resources individually.

## EXAMPLES

### Example 1: Create a VM with its networking stack

```powershell
New-VM -SubscriptionId "00000000-0000-0000-0000-000000000000" -Name "myvm" -Location "eastus" -ResourceGroupName "myrg" -VmSize "Standard_D2s_v3" -AdminUsername "azureuser" -AdminPassword "<password>" -NetworkInterfaceName "mynic" -VirtualNetworkName "myvnet" -NetworkSecurityGroupName "mynsg"
```

## PARAMETERS

### -AddressPrefixes

Address prefixes for the virtual network.

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

### -AdminPassword

The administrator password.

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

### -AdminUsername

The administrator username.

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

### -Location

Location for all resources.

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

The name of the virtual machine.

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

### -NetworkInterfaceName

The network interface name to create.

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

The network security group name to create.

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

### -RuleAccesses

Access types (Allow/Deny) of the security rules.

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

Destination address prefixes of the security rules.

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

Destination port ranges of the security rules.

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

Directions of the security rules.

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

Names of the security rules.

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

Priorities of the security rules.

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

Protocols of the security rules.

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

Source address prefixes of the security rules.

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

Source port ranges of the security rules.

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

Address prefixes for each subnet.

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

Names of the subnets.

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

### -VirtualNetworkName

The virtual network name to create.

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

### -VmSize

The size of the virtual machine.

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
Default value: None
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
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

## NOTES

Generated help for compute.

## RELATED LINKS
