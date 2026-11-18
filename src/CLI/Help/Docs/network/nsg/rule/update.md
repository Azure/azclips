---
summary: Update a security rule.
status: ga
schema: 1.0.0
online version:
---

Description: Update the properties of an existing security rule in a network security group. Only the options you give are changed.

### Change the priority and destination ports of a rule

```bash
az network nsg rule update --resource-group myrg --nsg-name mynsg --name allow-https \
  --priority 110 --destination-port-ranges 443 8443
```
