---
summary: Manage Microsoft Entra users through Microsoft Graph.
status: preview
schema: 1.0.0
online version:
---

Description: Query Microsoft Entra users via the Microsoft Graph `/users` endpoint. Use `az msgraph user show` to retrieve a single user by object ID or user principal name, or `az msgraph user list` for the first page of users in the signed-in tenant.

### Show a user by user principal name

```bash
az msgraph user show --id ada@contoso.com
```

### List users by display name prefix

```bash
az msgraph user list --display-name Ada
```
