---
summary: Show details for a virtual machine.
status: ga
schema: 1.0.0
online version:
---

Description: Retrieve the full property bag for the specified virtual machine, including hardware profile, OS profile, network interfaces, and provisioning state. Use this command to confirm a VM was created correctly or to inspect its current configuration.

### Show a virtual machine by name

```bash
az vm show --name myvm --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Show using shell variables

```bash
az vm show --name $vmName --resource-group $rg --subscription-id $subId
```

### Show several virtual machines by resource ID

```bash
az vm show --ids /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Compute/virtualMachines/myvm
```

`--ids` replaces `--name`, `--resource-group` and `--subscription-id`, and accepts more than one id. It always returns an array, one entry per id.

### Include the user data

```bash
az vm show --name myvm --resource-group myrg --include-user-data
```

Adds `properties.userData` to the result. Equivalent short form: `-u`.

### Show public IPs, FQDNs and power state

```bash
az vm show --name myvm --resource-group myrg --show-details
```

Fetches each network interface and public IP address the VM references, then adds `powerState`, `publicIps`, `fqdns`, `privateIps` and `macAddresses` to the result. This issues extra requests, so it is slower than a plain show. Equivalent short form: `-d`.
