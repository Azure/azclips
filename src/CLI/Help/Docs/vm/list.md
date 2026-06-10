---
summary: List virtual machines in a resource group.
status: ga
schema: 1.0.0
online version:
---

Description: Return a list of virtual machines that live in the specified resource group, including each machine's name, size, location, and provisioning state. Use this command for inventory, audits, or before bulk operations such as start, stop, or delete.

### List VMs in a resource group

```bash
az vm list --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000
```

### List using shell variables

```bash
az vm list --resource-group $rg --subscription-id $subId
```
