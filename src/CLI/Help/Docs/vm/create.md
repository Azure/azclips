---
summary: Create a virtual machine and its supporting network resources.
status: ga
schema: 1.0.0
online version:
---

Description: Provision an Azure virtual machine in the specified resource group along with the supporting virtual network, subnet, network security group, network interface, and public IP address in a single orchestrated call. The VM size, administrator credentials, and target location must all be provided. A resource name supplied for `--virtual-network-name` is created if it does not already exist in the resource group. The network interface is named `{vm}VMNic`; to attach existing network interfaces instead, use `--nics`. By default a network security group named `{vm}NSG` is created, or updated if it already exists. A network security group the command creates has one inbound rule for the image's operating system, allowing traffic from any source: `rdp` (TCP port 3389) for Windows or `default-allow-ssh` (TCP port 22) for Linux. Pass `--nsg` a name to reuse the network security group of that name, unchanged, when it exists and create it when it does not, a resource ID to attach an existing network security group, or `""` to create none. By default a Standard, static public IP address named `{vm}PublicIP` is created, or updated if it already exists. Pass `--public-ip-address` a name to reuse the public IP address of that name when it exists and create it when it does not, a resource ID to attach an existing public IP address, or `""` to create none. Use `--zone` to place the virtual machine in a single availability zone. Use `--storage-sku` to choose the storage SKU of the managed OS disk, as either `SKU` or `os=SKU`, such as `Premium_LRS`, `StandardSSD_LRS`, `Standard_LRS`, `Premium_ZRS`, or `StandardSSD_ZRS`; when omitted, the service default applies. Numeric data-disk IDs are not supported because this command does not create data disks. Use `--os-disk-delete-option` with `Delete` to delete the OS disk when the virtual machine is deleted, or `Detach` to keep it; when omitted, the service default (`Detach`) applies. Pass `--tags` to tag the virtual machine and every network resource the command creates; resources the command only attaches keep their own tags, such as network interfaces passed to `--nics`, the virtual network of an existing subnet selected with `--subnet`, or an existing network security group or public IP address. The command returns the new virtual machine with the power state and the public and private IP addresses that `az vm show -d` reports.

### Create a VM from a marketplace image URN

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --image MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest --admin-username azureuser --admin-password 'P@ssw0rd123!' --virtual-network-name myvm-vnet --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create using shell variables for credentials

```bash
az vm create --name $vmName --resource-group $rg --location eastus --size Standard_D2s_v5 --admin-username $adminUser --admin-password $adminPwd --virtual-network-name $vmName-vnet --subscription-id $subId
```

### Create a VM that uses an existing network security group

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --admin-username azureuser --admin-password 'P@ssw0rd123!' --virtual-network-name myvm-vnet --nsg mynsg
```

### Create with an SSH public key

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --image Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest --admin-username azureuser --admin-password 'P@ssw0rd123!' --ssh-key-values ~/.ssh/id_rsa.pub --virtual-network-name myvm-vnet --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create with a different image URN

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --image Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest --admin-username azureuser --admin-password 'P@ssw0rd123!' --virtual-network-name myvm-vnet --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a VM with Standard security type

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --image MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest --security-type Standard --admin-username azureuser --admin-password 'P@ssw0rd123!' --virtual-network-name myvm-vnet --nsg myvm-nsg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a Confidential VM

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_DC2as_v5 --image MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest --security-type ConfidentialVM --os-disk-security-encryption-type VMGuestStateOnly --admin-username azureuser --admin-password 'P@ssw0rd123!' --virtual-network-name myvm-vnet --nsg myvm-nsg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a Trusted Launch VM with secure boot disabled

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --image MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest --security-type TrustedLaunch --enable-secure-boot false --enable-vtpm true --admin-username azureuser --admin-password 'P@ssw0rd123!' --virtual-network-name myvm-vnet
```

### Create a VM with a named public IP address

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --admin-username azureuser --admin-password 'P@ssw0rd123!' --virtual-network-name myvm-vnet --public-ip-address myvm-ip
```

### Create a VM without a public IP address

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --admin-username azureuser --admin-password 'P@ssw0rd123!' --virtual-network-name myvm-vnet --public-ip-address ""
```

### Create a VM with tags

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --admin-username azureuser --admin-password 'P@ssw0rd123!' --virtual-network-name myvm-vnet --tags env=prod owner=team-a
```

### Create a VM in availability zone 1

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --zone 1 --admin-username azureuser --admin-password 'P@ssw0rd123!' --virtual-network-name myvm-vnet
```

### Create a VM with a Premium SSD OS disk

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --storage-sku Premium_LRS --admin-username azureuser --admin-password 'P@ssw0rd123!' --virtual-network-name myvm-vnet
```

### Create a VM whose OS disk is deleted with the VM

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --os-disk-delete-option Delete --admin-username azureuser --admin-password 'P@ssw0rd123!' --virtual-network-name myvm-vnet
```
