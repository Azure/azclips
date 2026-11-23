---
summary: Download a file to a local path.
status: preview
schema: 1.0.0
online version:
---

Description: Download the content of a file in a file share to a local file.

### Download a file

```bash
az storage file download --account-name mystorageaccount --account-key $ACCOUNT_KEY --share-name myshare --path mydir/myfile.txt --dest ./downloaded.txt
```
