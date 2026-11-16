---
summary: List Microsoft Entra users.
status: preview
schema: 1.0.0
online version:
---

Description: List users in your Microsoft Entra tenant. Filter results by a display name prefix, exact user principal name, or an OData `$filter` expression. You can also project custom fields with `--select`, include relationships with `--expand`, and use advanced OData options like `--orderby` and `--search`. For advanced filters (for example `endswith(...)`), include `--consistency-level eventual`.

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

### List users with custom projection and default fields

```bash
az msgraph user list --select id displayName jobTitle --append-selected
```

### Search users by display name

```bash
az msgraph user list --search "\"displayName:ada\"" --consistency-level eventual
```
