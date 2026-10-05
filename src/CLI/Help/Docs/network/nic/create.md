---
summary: Create a network interface.
status: ga
schema: 1.0.0
online version:
---

Description: Create a network interface with a single IP configuration in an existing subnet. Pass `--subnet` as a subnet resource id, or as a subnet name together with `--vnet-name`. The IP configuration is named `ipconfig1` and gets a dynamically allocated private IP address unless `--private-ip-address` is supplied, in which case the allocation is `Static`. `--public-ip-address` and `--network-security-group` accept a name in the same resource group or a full resource id.

### Create a network interface in a subnet

```bash
az network nic create --name mynic --resource-group myrg --location eastus --vnet-name myvnet --subnet default
```

### Create a network interface from a subnet resource id

```bash
az network nic create --name mynic --resource-group myrg --location eastus --subnet /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Network/virtualNetworks/myvnet/subnets/default
```

### Create a network interface with a public IP address and a network security group

```bash
az network nic create --name mynic --resource-group myrg --location eastus --vnet-name myvnet --subnet default \
    --public-ip-address mypip --network-security-group mynsg
```

### Create a network interface with a static private IP address

```bash
az network nic create --name mynic --resource-group myrg --location eastus --vnet-name myvnet --subnet default \
    --private-ip-address 10.0.0.10
```

### Create a network interface with accelerated networking, custom DNS servers and tags

```bash
az network nic create --name mynic --resource-group myrg --location eastus --vnet-name myvnet --subnet default \
    --accelerated-networking --dns-servers 10.0.0.4 10.0.0.5 --tags owner=team-a costCenter=42
```
