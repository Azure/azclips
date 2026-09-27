---
summary: Update a resource group.
status: ga
schema: 1.0.0
online version:
---

Description: Update a resource group without recreating it. Replace all tags with `--tags`, clear tags with an empty value, or modify an individual patchable property with `--set`. Use `--force-string` with `--set` when a value that resembles JSON, a number, or a boolean must remain a string.

### Replace the tags on a resource group

```bash
az group update --name myresourcegroup --tags Environment=Production Owner=Platform
```

### Clear all tags from a resource group

```bash
az group update --resource-group myresourcegroup --tags ""
```

### Update one tag while preserving its string value

```bash
az group update --name myresourcegroup --set tags.CostCenter=042 --force-string
```
