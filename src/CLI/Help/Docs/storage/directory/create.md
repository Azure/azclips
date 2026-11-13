---
summary: Create a directory in a file share.
status: preview
schema: 1.0.0
online version:
---

Description: Create a directory in a file share. Nested paths such as `parent/child` are supported, but the parent directory must already exist.

### Create a directory

```bash
az storage directory create --account-name mystorageaccount --account-key $ACCOUNT_KEY --share-name myshare --name mydir
```

### Create a nested directory

```bash
az storage directory create --account-name mystorageaccount --account-key $ACCOUNT_KEY --share-name myshare --name mydir/child
```
