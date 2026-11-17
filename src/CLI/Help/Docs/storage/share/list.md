---
summary: List file shares in a storage account.
status: preview
schema: 1.0.0
online version:
---

Description: List the file shares in a storage account, following continuation markers until all pages are returned. Use `--prefix` to filter by name.

### List file shares

```bash
az storage share list --account-name mystorageaccount --account-key $ACCOUNT_KEY
```

### List file shares whose names start with a prefix

```bash
az storage share list --account-name mystorageaccount --account-key $ACCOUNT_KEY --prefix logs
```
