---
summary: Wait for a management-group deployment condition.
status: preview
schema: 1.0.0
online version:
---

Description: Polls a management-group deployment until one requested existence or provisioning-state condition is met.

### Wait for successful provisioning

```bash
az deployment mg wait --management-group-id myManagementGroup --name myDeployment --updated
```
