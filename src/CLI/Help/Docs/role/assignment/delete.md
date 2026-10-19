---
summary: Delete a role assignment.
status: preview
schema: 1.0.0
online version:
---

Description: Delete an Azure RBAC role assignment by scope and name. The scope is the delete target: it defaults to the subscription and can be narrowed with `--resource-group` or overridden with `--scope` (the two are mutually exclusive). `--name` (the assignment's GUID) is required: this targets exactly one already-known assignment. Deleting by principal/role filter, or deleting every assignment at a scope, is not supported — use `az role assignment list` first to find the assignment's name.

### Delete a role assignment by name at a resource group scope

```bash
az role assignment delete --resource-group my-rg --name aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee
```

### Delete a role assignment at an explicit scope

```bash
az role assignment delete --scope /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/my-rg --name aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee
```
