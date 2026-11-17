---
summary: List management-group deployments.
status: preview
schema: 1.0.0
online version:
---

Description: Lists deployments recorded at the specified management-group scope. Use `--filter` to restrict results by provisioning state.

### List succeeded deployments

```bash
az deployment mg list --management-group-id myManagementGroup --filter "provisioningState eq 'Succeeded'"
```
