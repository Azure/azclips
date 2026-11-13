---
summary: Show the properties of a file.
status: preview
schema: 1.0.0
online version:
---

Description: Get the system properties and metadata of a file, such as its content length and content type, without downloading its content.

### Show a file

```bash
az storage file show --account-name mystorageaccount --account-key $ACCOUNT_KEY --share-name myshare --path mydir/myfile.txt
```
