---
summary: Wait for a management-group deployment condition.
status: preview
schema: 1.0.0
online version:
---

Description: Polls a management-group deployment until one requested existence or provisioning-state condition is met. The default interval is 30 seconds and the default timeout is 3600 seconds. If the timeout is reached before the condition is met, the command returns an empty JSON object (`{}`) with exit code 0, not a timeout error.

### Wait for successful provisioning

```bash
az deployment mg wait --management-group-id myManagementGroup --name myDeployment --updated
```
