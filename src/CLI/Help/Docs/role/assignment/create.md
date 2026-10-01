---
summary: Create a role assignment.
status: preview
schema: 1.0.0
online version:
---

Description: Create an Azure RBAC role assignment, granting a principal a role at a scope. The scope is the grant target (the subscription, resource group, or resource the access applies to): it defaults to the subscription and can be narrowed with `--resource-group` or overridden with `--scope` (the two are mutually exclusive). `--role` takes a role definition GUID or a fully-qualified role definition ID, and the principal (`--assignee-object-id` or `--assignee`) takes a GUID; role name and principal name/UPN/service-principal resolution are not yet supported.

### Create a role assignment at a resource group scope

```bash
az role assignment create --resource-group my-rg --role b24988ac-6180-42a0-ab88-20f7382dd24c --assignee-object-id 11111111-2222-3333-4444-555555555555
```

### Create a role assignment at an explicit scope

```bash
az role assignment create --scope /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/my-rg/providers/Microsoft.Storage/storageAccounts/my-account --role b24988ac-6180-42a0-ab88-20f7382dd24c --assignee-object-id 11111111-2222-3333-4444-555555555555
```

### Create a role assignment with a principal type and description

```bash
az role assignment create --scope /subscriptions/00000000-0000-0000-0000-000000000000 --role b24988ac-6180-42a0-ab88-20f7382dd24c --assignee-object-id 11111111-2222-3333-4444-555555555555 --assignee-principal-type ServicePrincipal --description "grant CI access"
```

`--assignee` is also accepted and currently takes the principal's object ID (name, UPN, and service-principal resolution are not yet supported):

```bash
az role assignment create --scope /subscriptions/00000000-0000-0000-0000-000000000000 --role b24988ac-6180-42a0-ab88-20f7382dd24c --assignee 11111111-2222-3333-4444-555555555555
```
