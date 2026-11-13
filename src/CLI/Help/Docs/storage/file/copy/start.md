---
summary: Start an asynchronous copy into a file.
status: preview
schema: 1.0.0
online version:
---

Description: Start a server-side copy into a file in a file share. The source must be an Azure file or blob; a source in another account must be readable, for example through a SAS token. The command returns once the copy is scheduled; check its status with `az storage file show`.

### Copy a file within the share

```bash
az storage file copy start --account-name mystorageaccount --account-key $ACCOUNT_KEY --share-name myshare --path mydir/copy.txt --source-uri https://mystorageaccount.file.core.windows.net/myshare/mydir/myfile.txt
```

### Copy a blob into a file

```bash
az storage file copy start --account-name mystorageaccount --account-key $ACCOUNT_KEY --share-name myshare --path mydir/fromblob.txt --source-uri https://mystorageaccount.blob.core.windows.net/mycontainer/myblob.txt
```
