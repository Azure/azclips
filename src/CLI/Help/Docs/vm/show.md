---
summary: Show details for a virtual machine.
status: ga
schema: 1.0.0
online version:
---

Description: Retrieve the full property bag for the specified virtual machine, including hardware profile, OS profile, network interfaces, and provisioning state. Use this command to confirm a VM was created correctly or to inspect its current configuration.

### Show a virtual machine by name

```bash
az vm show --name myvm --resource-group myrg
```

### Show with a different profile

```bash
az vm show --name myvm --resource-group myrg --profile dev
```
