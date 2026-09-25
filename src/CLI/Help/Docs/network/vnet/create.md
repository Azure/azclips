---
summary: Create a virtual network.
status: ga
schema: 1.0.0
online version:
---

Description: Provision an Azure virtual network in the specified resource group. Supply the VNet address space with `--address-prefixes`; when omitted it defaults to `10.0.0.0/16` unless `--ipam-allocations` is used. Subnets can be declared either with the compound `--subnets` argument (repeat it once per subnet, passing `KEY=VALUE` tokens) or with the positional `--subnet-names` / `--subnet-address-prefixes` pair, which are matched positionally: the first prefix list belongs to the first subnet name, and so on.

Supported `--subnets` keys: `name`, `address-prefix`, `address-prefixes`, `nsg`, `route-table`, `nat-gateway`, `default-outbound-access`, `service-endpoints`, `service-endpoint-policy`, `delegations`, `ipam-pool-prefix-allocations`, `disable-private-endpoint-network-policies`, `disable-private-link-service-network-policies`. Keys that accept several values take a comma-separated list.

### Create a virtual network with a single subnet

```bash
az network vnet create --name myvnet --resource-group myrg --location eastus --address-prefixes 10.0.0.0/16 --subnet-names default --subnet-address-prefixes 10.0.0.0/24
```

### Create a virtual network without subnets

```bash
az network vnet create --name myvnet --resource-group myrg --location eastus --address-prefixes 10.0.0.0/16
```

### Create several subnets with per-subnet settings

```bash
az network vnet create --name myvnet --resource-group myrg --location eastus --address-prefixes 10.0.0.0/16 \
  --subnets name=web address-prefixes=10.0.1.0/24 default-outbound-access=false \
  --subnets name=data address-prefixes=10.0.2.0/24 service-endpoints=Microsoft.Storage,Microsoft.Sql
```

### Delegate a subnet to a service

```bash
az network vnet create --name myvnet --resource-group myrg --location eastus --address-prefixes 10.0.0.0/16 \
  --subnets name=apps address-prefixes=10.0.3.0/24 delegations=Microsoft.Web/serverFarms
```

### Set DNS servers, tags, and a flow timeout

```bash
az network vnet create --name myvnet --resource-group myrg --location eastus --address-prefixes 10.0.0.0/16 \
  --dns-servers 10.0.0.4 10.0.0.5 --tags owner=team-a env=prod --flowtimeout 10
```

### Enable virtual network encryption

```bash
az network vnet create --name myvnet --resource-group myrg --location eastus --address-prefixes 10.0.0.0/16 \
  --enable-encryption --encryption-policy AllowUnencrypted
```
