---
summary: Get instance information about a virtual machine.
status: ga
schema: 1.0.0
online version:
---

Description: Retrieve a virtual machine together with its run-time instance view, returned at `properties.instanceView`: the provisioning and power states, the VM agent, disks, extensions, boot diagnostics, and patch status. The command makes one request, so it is a quick way to check whether a VM is running, stopped, or deallocated.

### Get the instance view of a virtual machine

```bash
az vm get-instance-view --name myvm --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Use the short option names

```bash
az vm get-instance-view -n myvm -g myrg
```

### Get the instance views of several virtual machines by resource ID

```bash
az vm get-instance-view --ids /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Compute/virtualMachines/myvm /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Compute/virtualMachines/myvm2
```

`--ids` replaces `--name`, `--resource-group` and `--subscription-id`, and accepts more than one id. It always returns an array, one entry per id.

### Show only the power state

```bash
az vm get-instance-view -n myvm -g myrg --query "properties.instanceView.statuses[?starts_with(code, 'PowerState/')].displayStatus"
```
