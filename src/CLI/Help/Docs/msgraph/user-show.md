---
summary: Show a Microsoft Graph user by ID or user principal name.
status: preview
schema: 1.0.0
online version:
---

Description: Retrieve a user object from Microsoft Graph using either the user's object ID or user principal name (UPN). The active Azure profile is used to authenticate to Microsoft Graph.

### Show a user by user principal name

```bash
az msgraph user show --id user@contoso.com
```

### Show a user by object ID

```bash
az msgraph user show --id 00000000-0000-0000-0000-000000000000
```
