---
summary: List Microsoft Entra groups.
status: preview
schema: 1.0.0
online version:
---

Description: List groups in your Microsoft Entra tenant. Filter results by a display name prefix or an OData `$filter` expression. Some advanced filters may require `--consistency-level eventual`.

### List all groups

```bash
az msgraph group list
```

### List groups whose display name starts with a prefix

```bash
az msgraph group list --display-name Sales
```

### List groups with a custom filter expression

```bash
az msgraph group list --filter "securityEnabled eq true" --consistency-level eventual
```

### List groups with a supported string filter expression

```bash
az msgraph group list --filter "startswith(displayName,'Team')"
```
