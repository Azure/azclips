---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Compute.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Compute
ms.date: 09/28/2026
PlatyPS schema version: 2024-05-01
title: New-AzVM
---

# New-AzVM

## SYNOPSIS

Create a virtual machine together with its network interface, public IP address, virtual network, and network security group in a single call.

## SYNTAX

### __AllParameterSets

```
New-AzVM -Name <string> -Location <string> -ResourceGroupName <string> -VmSize <string>
 -AdminUsername <string> -AdminPassword <string> -NetworkInterfaceName <string>
 -VirtualNetworkName <string> [-SubscriptionId <string>] [-Image <string>]
 [-SecurityGroupName <string>]
 [-SubnetNames <string[]>] [-SubnetAddressPrefixes <string[][]>] [-AddressPrefixes <string[]>]
 [-AcceleratedNetworking] [-PublicIpAddressName <string>] [-Tag <hashtable>] [-Profile <string>]
 [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Provisions the full networking + VM stack as a single operation.
Creates a virtual network, network security group, public IP address, network interface, and virtual machine using the supplied names and parameters.
By default the network security group is named after the virtual machine with an `NSG` suffix (for example `myvmNSG`), created or, if it already exists, updated.
A network security group this command creates has one inbound rule for the image's operating system, allowing traffic from any source: `rdp` (TCP port 3389) for Windows or `default-allow-ssh` (TCP port 22) for Linux.
Use `-SecurityGroupName` to choose the network security group by name or resource ID, or pass an empty string to create none.
By default the public IP address is a Standard, static address named after the virtual machine with a `PublicIP` suffix (for example `myvmPublicIP`), created or, if it already exists, updated.
Use `-PublicIpAddressName` to choose the public IP address by name or resource ID, or pass an empty string to create none.
Use `-Tag` to tag the virtual machine and each network resource this command creates or updates, including an existing virtual network named by `-VirtualNetworkName`, whose tags are replaced; an existing network security group or public IP address that this command attaches keeps its own tags.
Use this command when you want a one-shot VM deployment without composing the underlying resources individually.

## EXAMPLES

### Create a VM with its networking stack

Provision a `Standard_D2s_v3` virtual machine named `myvm` in `myrg` (East US) along with a fresh virtual network (`myvnet`), network interface (`mynic`), and network security group (`myvmNSG`, which allows inbound RDP).
Assign `$adminPassword` from a secure source in your own pipeline before invoking the cmdlet (the value below is a literal placeholder, not a real password).

```powershell
$adminPassword = 'P@ssw0rd-replace-me!'
New-AzVM -SubscriptionId 00000000-0000-0000-0000-000000000000 -Name myvm -Location eastus -ResourceGroupName myrg -VmSize Standard_D2s_v3 -Image MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest -AdminUsername azureuser -AdminPassword $adminPassword -NetworkInterfaceName mynic -VirtualNetworkName myvnet
```

### Create a VM that uses an existing network security group

Provision the same stack, but attach the existing network security group `mynsg` in `myrg` instead of creating one.
The network security group is attached as-is; its rules are not changed.

```powershell
$adminPassword = 'P@ssw0rd-replace-me!'
New-AzVM -Name myvm -Location eastus -ResourceGroupName myrg -VmSize Standard_D2s_v3 -AdminUsername azureuser -AdminPassword $adminPassword -NetworkInterfaceName mynic -VirtualNetworkName myvnet -SecurityGroupName mynsg
```

### Create a VM with a named public IP address

Provision the same stack, but name the public IP address `myvm-ip`.
If a public IP address named `myvm-ip` already exists in `myrg`, it is attached as-is instead of being created.

```powershell
$adminPassword = 'P@ssw0rd-replace-me!'
New-AzVM -Name myvm -Location eastus -ResourceGroupName myrg -VmSize Standard_D2s_v3 -AdminUsername azureuser -AdminPassword $adminPassword -NetworkInterfaceName mynic -VirtualNetworkName myvnet -PublicIpAddressName myvm-ip
```

### Create a VM without a public IP address

Pass an empty string to `-PublicIpAddressName` to provision the stack without a public IP address.
The virtual machine is then reachable only from inside its virtual network.

```powershell
$adminPassword = 'P@ssw0rd-replace-me!'
New-AzVM -Name myvm -Location eastus -ResourceGroupName myrg -VmSize Standard_D2s_v3 -AdminUsername azureuser -AdminPassword $adminPassword -NetworkInterfaceName mynic -VirtualNetworkName myvnet -PublicIpAddressName ''
```

### Create a VM with tags

Provision the same stack and tag the virtual machine, its network interface, virtual network, network security group, and public IP address with `env` and `owner`.

```powershell
$adminPassword = 'P@ssw0rd-replace-me!'
New-AzVM -Name myvm -Location eastus -ResourceGroupName myrg -VmSize Standard_D2s_v3 -AdminUsername azureuser -AdminPassword $adminPassword -NetworkInterfaceName mynic -VirtualNetworkName myvnet -Tag @{ env = 'prod'; owner = 'team-a' }
```

## PARAMETERS

### -AcceleratedNetworking

Enables accelerated networking on the network interface created for the virtual machine. Off
unless set; the chosen VM size must support accelerated networking.

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

### -AddressPrefixes

Address prefixes for the virtual network.

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

### -AdminPassword

The administrator password.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -AdminUsername

The administrator username.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Confirm

Prompts you for confirmation before running the cmdlet.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases:
- cf
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

### -Image

The marketplace image URN to use.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- ImageName
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

### -Location

Location for all resources.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Name

The name of the virtual machine.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -NetworkInterfaceName

The network interface name to create.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Profile

Name of a saved profile to use for this call instead of the currently-active one. Manage profiles with `Set-AzProfile` / `Get-AzProfile`.

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

### -PublicIpAddressName

Name of the public IP address to create (default: '{vm}PublicIP') or of an existing one to attach.
Can also reference an existing public IP by resource ID, or specify "" for none.

When omitted, a public IP address named after the virtual machine with a `PublicIP` suffix is created, or updated if it already exists.
A name is looked up in the resource group: an existing public IP address is attached as-is, otherwise one is created.
A resource ID must reference an existing public IP address, which is attached as-is.
New public IP addresses use the Standard SKU with static allocation, in the virtual machine's location.

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

The resource group name.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -SecurityGroupName

Name of the network security group to create (default: '{vm}NSG') or of an existing one to attach.
Can also reference an existing NSG by resource ID, or specify "" for none.

When omitted, a network security group named after the virtual machine with an `NSG` suffix is created, or updated if it already exists.
A name is looked up in the resource group: an existing network security group is attached as-is, otherwise one is created.
A resource ID must reference an existing network security group, which is attached as-is.
A network security group this command creates has one inbound rule for the image's operating system, allowing traffic from any source: `rdp` (TCP port 3389) for Windows or `default-allow-ssh` (TCP port 22) for Linux.

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

### -SubnetAddressPrefixes

Address prefixes for each subnet.

```yaml
Type: System.String[][]
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

### -SubnetNames

Names of the subnets.

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

Azure Subscription Id. Defaults to the active profile's subscription.

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

### -Tag

Key-value pairs in the form of a hash table, applied to the VM and to each network resource the command creates or updates.
For example: @{key0="value0";key1=$null;key2="value2"}.

Keys and values are stored as text, and a `$null` value as an empty string.
The tags go on the virtual machine, its network interface and virtual network, and on the network security group and public IP address when this command creates or updates them.
An existing virtual network named by `-VirtualNetworkName` is updated, so its tags are replaced.
An existing network security group or public IP address that this command attaches keeps its own tags.

```yaml
Type: System.Collections.Hashtable
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

### -VirtualNetworkName

The virtual network name to create.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -VmSize

The size of the virtual machine.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -WhatIf

Shows what would happen if the cmdlet runs.
The cmdlet is not run.
Runs the command in a mode that only reports what would happen without performing the actions.
Runs the command in a mode that only reports what would happen without performing the actions.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases:
- wi
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

### Microsoft.Azure.Azclips.Extensions.Compute.Models.VirtualMachine

The newly-created `VirtualMachine` object, including its assigned ARM resource ID, provisioning state, size, and network configuration, plus the `PowerState`, `PublicIps`, `Fqdns`, `PrivateIps`, and `MacAddresses` detail fields.
`Fqdns` is empty unless an attached existing public IP address has a DNS name label; public IP addresses this command creates have none.
`PublicIps` is empty when `-PublicIpAddressName` is an empty string.
`Tags` holds the virtual machine's tags as ARM returns them, and is `$null` when it has none.

## NOTES

Generated help for compute.

## RELATED LINKS


- [Online Version]()
