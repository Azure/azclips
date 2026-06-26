---
summary: Delete a virtual machine.
status: ga
schema: 1.0.0
online version:
---

Description: Remove the specified virtual machine from its resource group. The command deletes only the VM resource — attached disks, network interfaces, and the resource group itself are preserved and must be cleaned up separately if no longer required.

### Delete a virtual machine

```bash
az vm delete --name myvm --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Delete using shell variables

```bash
az vm delete --name $vmName --resource-group $rg --subscription-id $subId
```
