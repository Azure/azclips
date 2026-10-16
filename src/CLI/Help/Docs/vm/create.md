---
summary: Create a virtual machine and its supporting network resources.
status: ga
schema: 1.0.0
online version:
---

Description: Provision an Azure virtual machine in the specified resource group along with the supporting virtual network, subnet, network security group, network interface, and public IP address in a single orchestrated call. The VM size, administrator credentials, and target location must all be provided. Resource names supplied for `--virtual-network-name` and `--network-interface-name` are created if they do not already exist in the resource group. By default a network security group named `{vm}NSG` is created, or updated if it already exists. A network security group the command creates has one inbound rule for the image's operating system, allowing traffic from any source: `rdp` (TCP port 3389) for Windows or `default-allow-ssh` (TCP port 22) for Linux. Pass `--nsg` a name to reuse the network security group of that name, unchanged, when it exists and create it when it does not, a resource ID to attach an existing network security group, or `""` to create none. By default a Standard, static public IP address named `{vm}PublicIP` is created, or updated if it already exists. Pass `--public-ip-address` a name to reuse the public IP address of that name when it exists and create it when it does not, a resource ID to attach an existing public IP address, or `""` to create none. Pass `--tags` to tag the virtual machine and each network resource the command creates or updates, including an existing virtual network named by `--virtual-network-name`, whose tags are replaced; an existing network security group or public IP address that the command attaches keeps its own tags. The command returns the new virtual machine with the power state and the public and private IP addresses that `az vm show -d` reports.

### Create a VM from a marketplace image URN

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --image MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create using shell variables for credentials

```bash
az vm create --name $vmName --resource-group $rg --location eastus --size Standard_D2s_v5 --admin-username $adminUser --admin-password $adminPwd --network-interface-name $vmName-nic --virtual-network-name $vmName-vnet --subscription-id $subId
```

### Create a VM that uses an existing network security group

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --nsg mynsg
```

### Create with an SSH public key

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --image Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest --admin-username azureuser --admin-password 'P@ssw0rd123!' --ssh-key-values ~/.ssh/id_rsa.pub --network-interface-name myvm-nic --virtual-network-name myvm-vnet --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create with a different image URN

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --image Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a VM with Standard security type

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --image MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest --security-type Standard --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --nsg myvm-nsg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a Confidential VM

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_DC2as_v5 --image MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest --security-type ConfidentialVM --os-disk-security-encryption-type VMGuestStateOnly --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --nsg myvm-nsg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a VM with a named public IP address

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --public-ip-address myvm-ip
```

### Create a VM without a public IP address

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --public-ip-address ""
```

### Create a VM with tags

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --tags env=prod owner=team-a
```
