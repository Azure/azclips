---
summary: Delete a tenant deployment record.
status: preview
schema: 1.0.0
online version:
---

Description: Delete a tenant deployment history record and its operations. Resources created by the deployment are not deleted. The delete request is sent directly and the command waits for it to finish. Deleting a deployment that does not exist succeeds without output.

### Delete a tenant deployment record

```bash
az deployment tenant delete --name tenantBaseline
```
