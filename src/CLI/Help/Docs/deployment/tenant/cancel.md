---
summary: Cancel a running tenant-scope deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Cancel an active tenant deployment. The cancel request is sent directly; Azure Resource Manager rejects deployments that are not running, such as completed or missing deployments.

### Cancel a tenant deployment

```bash
az deployment tenant cancel --name tenantRollout
```
