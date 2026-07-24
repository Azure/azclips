---
summary: List Microsoft Entra application registrations.
status: preview
schema: 1.0.0
online version:
---

Description: Return the first page of application registrations from Microsoft Graph. Pagination is not followed in this preview — the response contains the default Graph page size. Use `--display-name` for a quick equality lookup, or `--filter` for any other OData expression.

### List the first page of applications

```bash
az msgraph app list
```

### List applications by display name

```bash
az msgraph app list --display-name my-app
```

### List applications with a raw OData filter

```bash
az msgraph app list --filter "signInAudience eq 'AzureADMyOrg'"
```
