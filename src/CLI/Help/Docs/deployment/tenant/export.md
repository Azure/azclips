---
summary: Export the template from a tenant-scope deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Export the ARM template saved with a tenant deployment. The command returns the template object. If the export response does not contain a template, the command reports an error and exits non-zero.

### Export a tenant deployment template

```bash
az deployment tenant export --name tenantBaseline
```
