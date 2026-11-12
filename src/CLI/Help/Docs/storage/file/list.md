---
summary: List files and directories in a file share.
status: preview
schema: 1.0.0
online version:
---

Description: List the files and directories in a directory, or in the share root when `--name` isn't given. Continuation markers are followed until all pages are returned.

### List the share root

```bash
az storage file list --account-name mystorageaccount --account-key $ACCOUNT_KEY --share-name myshare
```

### List a directory

```bash
az storage file list --account-name mystorageaccount --account-key $ACCOUNT_KEY --share-name myshare --name mydir
```
