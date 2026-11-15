---
summary: Manage files in Azure file shares.
status: preview
schema: 1.0.0
online version:
---

Description: Create, upload, download, list, inspect, copy, and delete files in a file share. Files are addressed by `--share-name` and `--path`, where the path includes any parent directories.

### Upload a file

```bash
az storage file upload --account-name mystorageaccount --account-key $ACCOUNT_KEY --share-name myshare --path mydir/myfile.txt --source ./myfile.txt
```

### List files and directories in the share root

```bash
az storage file list --account-name mystorageaccount --account-key $ACCOUNT_KEY --share-name myshare
```
