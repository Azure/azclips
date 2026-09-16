---
summary: Power off (stop) a running virtual machine.
status: ga
schema: 1.0.0
online version:
---

Description: Power off a running virtual machine while leaving its compute resources allocated. The virtual machine continues to be billed; use `az vm deallocate` to release the compute resources and stop compute charges. Target a single VM with `--name` and `--resource-group`, or pass one or more full resource IDs to `--ids` to power off several VMs in one invocation. When `--ids` is used it supplies the subscription, resource group, and VM name for each target, so `--name`, `--resource-group`, and `--subscription-id` are ignored. Pass `--skip-shutdown` to power the machine off immediately instead of requesting a graceful guest shutdown. The command waits for the long-running operation to finish and returns no output on success.

### Power off a running virtual machine

```bash
az vm stop --name myvm --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Power off a virtual machine without shutting down

```bash
az vm stop --name myvm --resource-group myrg --skip-shutdown
```

### Power off several virtual machines by resource ID

```bash
az vm stop --ids /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Compute/virtualMachines/myvm /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Compute/virtualMachines/myothervm
```
