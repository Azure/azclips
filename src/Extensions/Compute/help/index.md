---
summary: Manage compute resources (virtual networks, NSGs, network interfaces, and virtual machines).
status: ga
schema: 1.0.0
online version:
---

Description: Create and manage Azure compute resources. The compute extension provisions virtual machines along with their supporting networking stack (virtual network, network security group, and network interface) in a single call, and supports listing, showing, and deleting individual virtual machines.

### Create a VM with its networking stack (CLI)

```bash
azv2 vm create --name myvm --location eastus --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000 --vm-size Standard_D2s_v3 --admin-username azureuser --admin-password <password> --network-interface-name mynic --virtual-network-name myvnet --network-security-group-name mynsg
```

### Show a VM (CLI)

```bash
azv2 vm show --name myvm --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000
```

### List all VMs in a resource group (CLI)

```bash
azv2 vm list --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Delete a VM (CLI)

```bash
azv2 vm delete --name myvm --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a VM (PowerShell)

```powershell
New-VM -SubscriptionId "00000000-0000-0000-0000-000000000000" -Name "myvm" -Location "eastus" -ResourceGroupName "myrg" -VmSize "Standard_D2s_v3" -AdminUsername "azureuser" -AdminPassword "<password>" -NetworkInterfaceName "mynic" -VirtualNetworkName "myvnet" -NetworkSecurityGroupName "mynsg"
```

### Get a VM (PowerShell)

```powershell
Get-VM -SubscriptionId "00000000-0000-0000-0000-000000000000" -ResourceGroupName "myrg" -Name "myvm"
```

### Remove a VM (PowerShell)

```powershell
Remove-VM -SubscriptionId "00000000-0000-0000-0000-000000000000" -Name "myvm" -ResourceGroupName "myrg"
```
