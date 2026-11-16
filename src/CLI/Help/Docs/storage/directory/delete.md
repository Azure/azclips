---
summary: Delete an empty directory.
status: preview
schema: 1.0.0
online version:
---

Description: Delete a directory from a file share. The directory must be empty.

### Delete a directory

```bash
az storage directory delete --account-name mystorageaccount --account-key $ACCOUNT_KEY --share-name myshare --name mydir
```
