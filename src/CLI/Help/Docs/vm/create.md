---
summary: Create a virtual machine and its supporting network resources.
status: ga
schema: 1.0.0
online version:
---

Description: Provision an Azure virtual machine in the specified resource group along with the supporting virtual network, subnet, network security group, and network interface in a single orchestrated call. The VM size, administrator credentials, and target location must all be provided. Resource names supplied for `--virtual-network-name`, `--network-security-group-name`, and `--network-interface-name` are created if they do not already exist in the resource group.

### Create a VM from a marketplace image URN

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --image MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --network-security-group-name myvm-nsg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create using shell variables for credentials

```bash
az vm create --name $vmName --resource-group $rg --location eastus --size Standard_D2s_v5 --admin-username $adminUser --admin-password $adminPwd --network-interface-name $vmName-nic --virtual-network-name $vmName-vnet --network-security-group-name $vmName-nsg --subscription-id $subId
```

### Create with a different image URN

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --image Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --network-security-group-name myvm-nsg --subscription-id 00000000-0000-0000-0000-000000000000
```
