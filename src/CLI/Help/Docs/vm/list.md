---
summary: List virtual machines.
status: ga
schema: 1.0.0
online version:
---

Description: Return a list of virtual machines, including each machine's name, size, location, and provisioning state. Omit `--resource-group` to list every virtual machine in the subscription. `--show-details` runs one extra request per virtual machine, so it is slow, and fetches details for at most 100 virtual machines. Use this command for inventory, audits, or before bulk operations such as start, stop, or delete.

### List all VMs in the subscription

```bash
az vm list --subscription-id 00000000-0000-0000-0000-000000000000
```

### List VMs in a resource group

```bash
az vm list --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000
```

### List VM instances in a scale set by name

```bash
az vm list --resource-group myrg --vmss myvmss
```

### List VM instances in a scale set by resource id

```bash
az vm list --vmss /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Compute/virtualMachineScaleSets/myvmss
```

### List VMs with public IP addresses, FQDNs and power states

```bash
az vm list --resource-group myrg -d
```

### List using shell variables

```bash
az vm list --resource-group $rg --subscription-id $subId
```
