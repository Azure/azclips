---
summary: Start a stopped virtual machine.
status: ga
schema: 1.0.0
online version:
---

Description: Power on a stopped or deallocated virtual machine. Target a single VM with `--name` and `--resource-group`, or pass one or more full resource IDs to `--ids` to start several VMs in one invocation. When `--ids` is used it supplies the subscription, resource group, and VM name for each target, so `--name`, `--resource-group`, and `--subscription-id` are ignored. The command waits for the long-running start operation to finish and returns no output on success.

### Start a virtual machine

```bash
az vm start --name myvm --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Start using shell variables

```bash
az vm start --name $vmName --resource-group $rg --subscription-id $subId
```

### Start several virtual machines by resource ID

```bash
az vm start --ids /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Compute/virtualMachines/myvm /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Compute/virtualMachines/myothervm
```
