---
summary: List blobs in a container.
status: preview
schema: 1.0.0
online version:
---

Description: List the blobs in a container, following continuation markers until all pages are returned. Use `--prefix` to filter by name.

### List blobs in a container

```bash
az storage blob list --account-name mystorageaccount --account-key $ACCOUNT_KEY --container-name mycontainer
```

### List blobs with a name prefix

```bash
az storage blob list --account-name mystorageaccount --account-key $ACCOUNT_KEY --container-name mycontainer --prefix logs/
```
