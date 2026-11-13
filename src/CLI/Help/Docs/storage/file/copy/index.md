---
summary: Manage asynchronous file copy operations.
status: preview
schema: 1.0.0
online version:
---

Description: Start a server-side copy into a file from another Azure file or blob. The copy runs asynchronously; check its progress with `az storage file show`.

### Start a file copy

```bash
az storage file copy start --account-name mystorageaccount --account-key $ACCOUNT_KEY --share-name myshare --path mydir/copy.txt --source-uri https://mystorageaccount.file.core.windows.net/myshare/mydir/myfile.txt
```
