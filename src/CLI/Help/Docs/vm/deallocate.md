---
summary: Deallocate a virtual machine and release its compute resources.
status: ga
schema: 1.0.0
online version:
---

Description: Shut down a virtual machine and release its compute resources, so you are no longer billed for compute. Use `az vm stop` instead to power the machine off while keeping it allocated. Target a single VM with `--name` and `--resource-group`, or pass one or more full resource IDs to `--ids` to deallocate several VMs in one invocation. When `--ids` is used it supplies the subscription, resource group, and VM name for each target, so `--name`, `--resource-group`, and `--subscription-id` are ignored. Pass `--hibernate` to preserve the machine's memory state, or `--force-deallocate` to force the operation. The command waits for the long-running operation to finish and returns no output on success.

### Deallocate a virtual machine

```bash
az vm deallocate --name myvm --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Hibernate a virtual machine while deallocating it

```bash
az vm deallocate --name myvm --resource-group myrg --hibernate
```

### Force deallocate a virtual machine

```bash
az vm deallocate --name myvm --resource-group myrg --force-deallocate
```

### Deallocate several virtual machines by resource ID

```bash
az vm deallocate --ids /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Compute/virtualMachines/myvm /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Compute/virtualMachines/myothervm
```
