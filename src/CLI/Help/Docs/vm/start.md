---
summary: Start a virtual machine.
status: ga
schema: 1.0.0
online version:
---

Description: Start the specified virtual machine. The command is a long-running operation — by default it polls until the platform reports the start has completed and produces no output on success. Pass `--no-wait` to return as soon as Azure accepts the request instead of waiting. Starting an already-running virtual machine is a no-op.

Provide either `--ids` or the `--name`/`--resource-group` pair. When several IDs are supplied the virtual machines are started concurrently, up to five at a time. Every ID is attempted even if an earlier one fails; each failure is reported on its own, followed by a summary naming the virtual machines that could not be started.

### Start a virtual machine

```bash
az vm start --name myvm --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Start using shell variables

```bash
az vm start --name $vmName --resource-group $rg --subscription-id $subId
```

### Start a virtual machine without waiting for it to finish

```bash
az vm start --name myvm --resource-group myrg --no-wait
```

### Start a virtual machine by resource ID

```bash
az vm start --ids /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Compute/virtualMachines/myvm
```

### Start several virtual machines at once

```bash
az vm start --no-wait --ids $(az vm list --resource-group myrg --query "[].id" -o tsv)
```
