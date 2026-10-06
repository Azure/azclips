---
summary: Create a public IP address.
status: ga
schema: 1.0.0
online version:
---

Description: Create a public IP address in a resource group. The SKU defaults to `Standard`, the IP version to `IPv4` and the idle timeout to 4 minutes. The allocation method defaults to `Static` for the Standard SKUs and `Dynamic` for the Basic SKU.

### Create a public IP address

```bash
az network public-ip create --name mypip --resource-group myrg --location eastus
```

### Create a zone-redundant Standard address

```bash
az network public-ip create --name mypip --resource-group myrg --location eastus \
    --sku Standard --zone 1 2 3
```

### Create an address with a DNS name and tags

```bash
az network public-ip create --name mypip --resource-group myrg --location eastus \
    --dns-name mylabel --tags owner=team-a costCenter=42
```

### Create a Basic dynamic IPv6 address

```bash
az network public-ip create --name mypip --resource-group myrg --location eastus \
    --sku Basic --version IPv6 --allocation-method Dynamic
```

### Allocate from a public IP prefix

```bash
az network public-ip create --name mypip --resource-group myrg --location eastus \
    --public-ip-prefix myprefix
```

### Create an address with DDoS protection and an IP tag

```bash
az network public-ip create --name mypip --resource-group myrg --location eastus \
    --protection-mode Enabled --ddos-protection-plan myplan \
    --ip-tags FirstPartyUsage=/Sql
```
