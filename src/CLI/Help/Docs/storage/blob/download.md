---
summary: Download a blob to a local file.
status: preview
schema: 1.0.0
online version:
---

Description: Download the content of a blob to a local file. Use `--snapshot` or `--version-id` to download a specific snapshot or version.

### Download a blob

```bash
az storage blob download --account-name mystorageaccount --account-key $ACCOUNT_KEY --container-name mycontainer --name myblob.txt --file ./downloaded.txt
```
