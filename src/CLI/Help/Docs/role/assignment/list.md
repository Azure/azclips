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

### Include assignments inherited from ancestor scopes

By default only assignments made at the exact scope are listed. Use `--include-inherited` to also include assignments inherited from parent scopes (for example a management-group grant effective at the subscription).

```bash
az role assignment list --include-inherited
```

### List across the whole subscription including nested scopes

`--all` lists role assignments at the subscription and every nested scope (resource groups and resources) beneath it. It cannot be combined with `--scope`, `--resource-group`, or `--include-inherited` (which widens in the opposite direction, toward ancestor scopes).

```bash
az role assignment list --all
```

### Include assignments granted through group membership

When filtering by assignee, `--include-groups` also returns assignments the principal receives transitively through the groups it belongs to. It requires an assignee (`--assignee-object-id`).

```bash
az role assignment list --assignee-object-id 11111111-2222-3333-4444-555555555555 --include-groups
```
