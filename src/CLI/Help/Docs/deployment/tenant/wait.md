---
summary: Wait for a tenant-scope deployment condition.
status: preview
schema: 1.0.0
online version:
---

Description: Poll a tenant deployment until exactly one condition is met: `--created`, `--updated`, `--deleted`, `--exists`, or a `--custom` JMESPath query. Use `--interval` (default 30 seconds) and `--timeout` (default 3600 seconds) to control polling. The command returns no output when the condition is met and fails if the deployment reaches the Failed state.

### Wait for a deployment to succeed

```bash
az deployment tenant wait --name tenantRollout --updated --interval 10 --timeout 1800
```

### Wait for a deployment to be deleted

```bash
az deployment tenant wait --name tenantRollout --deleted
```
