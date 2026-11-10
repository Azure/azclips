---
summary: Manage Microsoft Entra users.
status: preview
schema: 1.0.0
online version:
---

Description: Look up or list users in your Microsoft Entra tenant.

### Show the signed-in user

```bash
az msgraph user signed-in
```

### List users in the signed-in tenant

```bash
az msgraph user list
```

### Show a user by user principal name

```bash
az msgraph user show --id ada@contoso.com
```
