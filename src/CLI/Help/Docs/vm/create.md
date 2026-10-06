---
summary: Create a virtual machine and its supporting network resources.
status: ga
schema: 1.0.0
online version:
---

Description: Provision an Azure virtual machine in the specified resource group along with the supporting virtual network, subnet, network security group, network interface, and public IP address in a single orchestrated call. The VM size, administrator credentials, and target location must all be provided. Resource names supplied for `--virtual-network-name`, `--network-security-group-name`, and `--network-interface-name` are created if they do not already exist in the resource group. By default a Standard, static public IP address named `{vm}PublicIP` is created, or updated if it already exists. Pass `--public-ip-address` a name to reuse the public IP address of that name when it exists and create it when it does not, a resource ID to attach an existing public IP address, or `""` to create none. The command returns the new virtual machine with the power state and the public and private IP addresses that `az vm show -d` reports.

### Create a VM from a marketplace image URN

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --image MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --network-security-group-name myvm-nsg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create using shell variables for credentials

```bash
az vm create --name $vmName --resource-group $rg --location eastus --size Standard_D2s_v5 --admin-username $adminUser --admin-password $adminPwd --network-interface-name $vmName-nic --virtual-network-name $vmName-vnet --network-security-group-name $vmName-nsg --subscription-id $subId
```

### Create with an SSH public key

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --image Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest --admin-username azureuser --admin-password 'P@ssw0rd123!' --ssh-key-values ~/.ssh/id_rsa.pub --network-interface-name myvm-nic --virtual-network-name myvm-vnet --network-security-group-name myvm-nsg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create with a different image URN

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --image Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --network-security-group-name myvm-nsg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a VM with Standard security type

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --image MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest --security-type Standard --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --network-security-group-name myvm-nsg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a Confidential VM

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --image MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest --security-type ConfidentialVM --os-disk-security-encryption-type VMGuestStateOnly --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --network-security-group-name myvm-nsg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a VM with a named public IP address

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --network-security-group-name myvm-nsg --public-ip-address myvm-ip
```

### Create a VM without a public IP address

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --network-security-group-name myvm-nsg --public-ip-address ""
```
