---
summary: Cancel a running tenant deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Cancels a tenant deployment whose provisioning state is `Accepted` or `Running`. Resources the deployment already created are left in place. Azure Resource Manager rejects the request, and the command fails, when the deployment is no longer running.

### Cancel a running deployment

```bash
az deployment tenant cancel --name myDeployment
```
