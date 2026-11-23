---
summary: Show the properties of a blob.
status: preview
schema: 1.0.0
online version:
---

Description: Get the system properties and user-defined metadata of a blob without downloading its content. Use `--snapshot` or `--version-id` to read a specific snapshot or version.

### Show a blob

```bash
az storage blob show --account-name mystorageaccount --account-key $ACCOUNT_KEY --container-name mycontainer --name myblob.txt
```

### Show a blob by URL with a SAS token

```bash
az storage blob show --url https://mystorageaccount.blob.core.windows.net/mycontainer/myblob.txt --sas-token $SAS_TOKEN
```
