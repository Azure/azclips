---
summary: Get the details of a Microsoft Entra user.
status: preview
schema: 1.0.0
online version:
---

Description: Retrieve full details for a user by object ID or user principal name.

### Show a user by user principal name

```bash
az msgraph user show --id user@contoso.com
```

### Show a user by object ID

```bash
az msgraph user show --id 00000000-0000-0000-0000-000000000000
```

### Show a user with selected fields only

```bash
az msgraph user show --id user@contoso.com --select id displayName mail
```

### Show a user with selected fields plus default fields

```bash
az msgraph user show --id user@contoso.com --select id displayName mail --append-selected
```
