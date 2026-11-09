---
summary: List Microsoft Entra users.
status: preview
schema: 1.0.0
online version:
---

Description: List users in your Microsoft Entra tenant. Filter results by a display name prefix, exact user principal name, or an OData `$filter` expression. Use `--consistency-level eventual` for advanced Graph filters such as `endswith(...)`.

### List all users

```bash
az msgraph user list
```

### List users whose display name starts with a prefix

```bash
az msgraph user list --display-name Ada
```

### List a user by user principal name

```bash
az msgraph user list --upn ada@contoso.com
```

### List users with a custom filter expression

```bash
az msgraph user list --filter "endswith(mail,'@contoso.com')" --consistency-level eventual
```
