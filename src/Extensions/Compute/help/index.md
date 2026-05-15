---
summary: Manage compute resources (virtual networks, NSGs, network interfaces, and virtual machines).
status: ga
schema: 1.0.0
online version:
---

Description: Create and manage Azure compute resources. The compute extension provisions virtual networks, network security groups, network interfaces, and virtual machines, as well as combined commands that create the full networking + VM stack in a single call.

### Create a VM with combined networking (CLI)

```bash
azv2 vm create-combined --name myvm --location eastus --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000 --vm-size Standard_D2s_v3 --admin-username azureuser --admin-password <password> --network-interface-name mynic --virtual-network-name myvnet --network-security-group-name mynsg
```

### Create a virtual network (PowerShell)

```powershell
New-VirtualNetwork -SubscriptionId "00000000-0000-0000-0000-000000000000" -Name "myvnet" -Location "eastus" -ResourceGroupName "myrg"
```

### Create a network security group (PowerShell)

```powershell
New-NetworkSecurityGroup -SubscriptionId "00000000-0000-0000-0000-000000000000" -Name "mynsg" -Location "eastus" -ResourceGroupName "myrg"
```

### Create a VM (PowerShell)

```powershell
New-VM -SubscriptionId "00000000-0000-0000-0000-000000000000" -Name "myvm" -Location "eastus" -ResourceGroupName "myrg" -VmSize "Standard_D2s_v3" -AdminUsername "azureuser" -AdminPassword "<password>" -NetworkInterfaceId "/subscriptions/.../networkInterfaces/mynic"
```
