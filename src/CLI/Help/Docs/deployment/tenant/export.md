---
summary: Export the template used by a tenant deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Exports the template used by a tenant deployment and returns only the template itself, without the `template` wrapper. The command fails if the service response contains no template.

### Export a deployment template

```bash
az deployment tenant export --name myDeployment
```
