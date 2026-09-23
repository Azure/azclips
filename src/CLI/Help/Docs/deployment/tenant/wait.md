---
summary: Wait for a tenant-scope deployment condition.
status: preview
schema: 1.0.0
online version:
---

Description: Poll a tenant deployment until it is created, deleted, exists, succeeds, or reaches a custom provisioning state. Use `--interval` and `--timeout` to control polling.

### Wait for a deployment to succeed

```bash
az deployment tenant wait --name tenantRollout --updated --interval 10 --timeout 1800
```
