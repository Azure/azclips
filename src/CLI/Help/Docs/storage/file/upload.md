---
summary: Upload a local file to a file share.
status: preview
schema: 1.0.0
online version:
---

Description: Upload a local file to a path in a file share. The parent directory must already exist.

### Upload a file

```bash
az storage file upload --account-name mystorageaccount --account-key $ACCOUNT_KEY --share-name myshare --path mydir/myfile.txt --source ./myfile.txt
```
