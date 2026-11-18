---
summary: Create a security rule.
status: ga
schema: 1.0.0
online version:
---

Description: Create a security rule in a network security group. When omitted, access is Allow, direction is Inbound, protocol is *, and the source and destination are *. Destination port 80 is used when no destination port is given.

### Allow inbound HTTPS from the internet

```bash
az network nsg rule create --resource-group myrg --nsg-name mynsg --name allow-https --priority 100 \
  --protocol Tcp --source-address-prefixes Internet --destination-port-ranges 443
```

### Deny outbound traffic to a range of addresses

```bash
az network nsg rule create --resource-group myrg --nsg-name mynsg --name deny-out --priority 200 \
  --direction Outbound --access Deny --destination-address-prefixes 10.1.0.0/16
```
