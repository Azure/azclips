---
summary: List role assignments.
status: preview
schema: 1.0.0
online version:
---

Description: List Azure RBAC role assignments at a subscription, resource group, or resource scope. The scope defaults to the active subscription.

### List role assignments at the subscription scope

```bash
az role assignment list
```

### List role assignments scoped to a resource group

```bash
az role assignment list --resource-group my-rg
```

### List role assignments at an explicit scope

```bash
az role assignment list --scope /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/my-rg
```

### List role assignments for a specific principal

```bash
az role assignment list --assignee-object-id 11111111-2222-3333-4444-555555555555
```

`--assignee` is also accepted and currently takes the principal's object ID (name, UPN, and service-principal resolution are not yet supported):

```bash
az role assignment list --assignee 11111111-2222-3333-4444-555555555555
```

### List role assignments for a specific role

`--role` currently takes a role definition ID (GUID); role name resolution is not yet supported.

```bash
az role assignment list --role b24988ac-6180-42a0-ab88-20f7382dd24c
```
