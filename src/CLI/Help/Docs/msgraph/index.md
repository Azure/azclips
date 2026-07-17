---
summary: Query Microsoft Graph.
status: preview
schema: 1.0.0
online version:
---

Description: Query Microsoft Graph resources such as Entra ID users through the authenticated Azure profile. Use `msgraph user show` to look up a user by object ID or user principal name.

### Show a Microsoft Graph user by UPN

```bash
az msgraph user show --id user@contoso.com
```

### Show a Microsoft Graph user by object ID

```bash
az msgraph user show --id 00000000-0000-0000-0000-000000000000
```
