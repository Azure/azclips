---
summary: List Microsoft Entra application registrations.
status: preview
schema: 1.0.0
online version:
---

Description: Return application registrations from Microsoft Graph. All pages are returned, following the Graph `@odata.nextLink` continuation. Use `--display-name` for a quick equality lookup, or `--filter` for any other OData expression.

### List applications

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
