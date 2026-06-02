---
summary: Delete a virtual machine.
status: ga
schema: 1.0.0
online version:
---

Description: Deletes the named virtual machine from the specified resource group and subscription. This operation removes only the VM resource itself; associated disks, network interfaces, and other dependent resources are not deleted.

### Delete a VM

```bash
azv2 vm delete --name myvm --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000
```
