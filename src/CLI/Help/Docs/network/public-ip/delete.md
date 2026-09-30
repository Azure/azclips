---
summary: Delete a public IP address.
status: ga
schema: 1.0.0
online version:
---

Description: Delete the named public IP address. The delete fails while the address is still associated with another resource, so detach it from the network interface, load balancer or gateway first.

### Delete a public IP address

```bash
az network public-ip delete --name mypip --resource-group myrg
```
