---
summary: Query Microsoft Graph.
status: preview
schema: 1.0.0
online version:
---

Description: Query Microsoft Graph resources such as Microsoft Entra users through the authenticated Azure profile. Use `msgraph user list` to enumerate users in the signed-in tenant, or `msgraph user show` to look up a single user by object ID or user principal name.

### List users in the signed-in tenant

```bash
az msgraph user list
```

### Show a Microsoft Graph user by UPN

```bash
az msgraph user show --id user@contoso.com
```

### Show a Microsoft Graph user by object ID

```bash
az msgraph user show --id 00000000-0000-0000-0000-000000000000
```
