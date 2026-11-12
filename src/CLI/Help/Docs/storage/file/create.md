---
summary: Create an empty file.
status: preview
schema: 1.0.0
online version:
---

Description: Create a file in a file share without uploading content. Use `--size` to set the file length in bytes; the content is zero-filled.

### Create an empty file

```bash
az storage file create --account-name mystorageaccount --account-key $ACCOUNT_KEY --share-name myshare --path mydir/empty.txt
```

### Create a 1 KiB file

```bash
az storage file create --account-name mystorageaccount --account-key $ACCOUNT_KEY --share-name myshare --path mydir/sized.bin --size 1024
```
