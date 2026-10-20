---
summary: Export the template from a tenant-scope deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Export the ARM template saved with a tenant deployment. The command returns the template object, or `null` when the deployment has no saved template.

### Export a tenant deployment template

```bash
az deployment tenant export --name tenantBaseline
```
