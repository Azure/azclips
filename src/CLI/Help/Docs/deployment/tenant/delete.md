---
summary: Delete a tenant deployment from the deployment history.
status: preview
schema: 1.0.0
online version:
---

Description: Deletes a deployment and its deployment operations from the tenant deployment history and waits for the deletion to finish. Resources the deployment created are not deleted. A deployment that is still running cannot be deleted.

### Delete a deployment

```bash
az deployment tenant delete --name myDeployment
```
