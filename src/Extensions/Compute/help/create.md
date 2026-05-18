---
summary: Create a virtual machine together with its network interface, virtual network, and network security group in a single call.
status: ga
schema: 1.0.0
online version:
---

Description: Provisions the full networking + VM stack as a single operation. Creates a virtual network, network security group, network interface, and virtual machine using the supplied names and parameters. Use this command when you want a one-shot VM deployment without composing the underlying resources individually.

### Create a VM with its networking stack (CLI)

```bash
azv2 vm create --name myvm --location eastus --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000 --vm-size Standard_D2s_v3 --admin-username azureuser --admin-password <password> --network-interface-name mynic --virtual-network-name myvnet --network-security-group-name mynsg
```

### Create a VM with explicit subnet and address prefixes (CLI)

```bash
azv2 vm create --name myvm --location eastus --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000 --vm-size Standard_D2s_v3 --admin-username azureuser --admin-password <password> --network-interface-name mynic --virtual-network-name myvnet --network-security-group-name mynsg --subnet-names default --address-prefixes 10.0.0.0/16
```

### Create a VM (PowerShell)

```powershell
New-VM -SubscriptionId "00000000-0000-0000-0000-000000000000" -Name "myvm" -Location "eastus" -ResourceGroupName "myrg" -VmSize "Standard_D2s_v3" -AdminUsername "azureuser" -AdminPassword "<password>" -NetworkInterfaceName "mynic" -VirtualNetworkName "myvnet" -NetworkSecurityGroupName "mynsg"
```
