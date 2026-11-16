---
summary: Manage Azure file shares.
status: preview
schema: 1.0.0
online version:
---

Description: Create, show, list, update, and delete file shares by using the Azure Files data-plane API. Authenticate with `--account-name` and `--account-key`, or with `--sas-token`.

### Create a file share

```bash
az storage share create --account-name mystorageaccount --account-key $ACCOUNT_KEY --name myshare
```

### List file shares

```bash
az storage share list --account-name mystorageaccount --account-key $ACCOUNT_KEY
```
