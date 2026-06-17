---
summary: Manage Azure managed disks.
status: ga
schema: 1.0.0
online version:
---

Description: View and manage Azure managed disks attached to (or available for) virtual machines in a subscription. Use the `disk` command group to inventory disks scoped to a resource group; the listing surfaces disk name, size, SKU, and current attachment state so you can plan attach, detach, resize, or cleanup operations.

### Discover disk commands

```bash
azv2 disk --help
```

### List disks in a resource group

```bash
azv2 disk list --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000
```
