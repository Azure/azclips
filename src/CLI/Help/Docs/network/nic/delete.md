---
summary: Delete a network interface.
status: ga
schema: 1.0.0
online version:
---

Description: Delete the named network interface. The delete fails while the interface is still attached to a virtual machine, so detach it or delete the virtual machine first.

### Delete a network interface

```bash
az network nic delete --name mynic --resource-group myrg
```
