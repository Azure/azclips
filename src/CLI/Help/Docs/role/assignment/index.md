---
summary: Manage Azure role assignments.
status: preview
schema: 1.0.0
online version:
---

Description: Manage role assignments at a subscription, resource group, or resource scope.

### List role assignments at the subscription scope

```bash
az role assignment list
```

### List role assignments scoped to a resource group

```bash
az role assignment list --resource-group my-rg
```

### List role assignments for a specific principal

```bash
az role assignment list --assignee-object-id 11111111-2222-3333-4444-555555555555
```
