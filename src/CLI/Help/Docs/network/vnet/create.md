---
summary: Create a virtual network.
status: ga
schema: 1.0.0
online version:
---

Description: Provision an Azure virtual network in the specified resource group. Supply the VNet address space with `--address-prefixes`; when omitted it defaults to `10.0.0.0/16`. When `--location` is omitted, the location of the resource group is used. To create one subnet in the same request, give `--subnet-name` and, optionally, `--subnet-prefixes`. When `--subnet-prefixes` is omitted, the subnet uses the first VNet prefix with a /24 mask. Use `az network vnet subnet create` to add more subnets later.

### Create a virtual network with a single subnet

```bash
az network vnet create --name myvnet --resource-group myrg --location eastus --address-prefixes 10.0.0.0/16 --subnet-name default --subnet-prefixes 10.0.0.0/24
```

### Create a virtual network without subnets

```bash
az network vnet create --name myvnet --resource-group myrg --location eastus --address-prefixes 10.0.0.0/16
```

### Set DNS servers, tags, and a flow timeout

```bash
az network vnet create --name myvnet --resource-group myrg --dns-servers 10.0.0.4 10.0.0.5 --tags owner=team-a env=prod --flowtimeout 10
```

### Enable virtual network encryption

```bash
az network vnet create --name myvnet --resource-group myrg --enable-encryption --encryption-policy AllowUnencrypted
```