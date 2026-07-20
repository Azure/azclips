---
summary: List Microsoft Graph users in the signed-in tenant.
status: preview
schema: 1.0.0
online version:
---

Description: Return the first page of users from Microsoft Graph. Pagination via `@odata.nextLink` is not followed in this preview — the response contains the default Graph page size (typically 100 users). Use `--display-name` or `--upn` for quick equality lookups on the two most common fields, or `--filter` for any other OData expression. Mirrors the surface of `az ad user list`.

### List the first page of users

```bash
az msgraph user list
```

### List users by display name

```bash
az msgraph user list --display-name Ada
```

### List a user by user principal name

```bash
az msgraph user list --upn ada@contoso.com
```

### Filter users with a raw OData expression

```bash
az msgraph user list --filter "startswith(displayName,'Ada')"
```
